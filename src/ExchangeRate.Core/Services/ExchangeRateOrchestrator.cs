using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExchangeRate.Core.Entities;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Exceptions;
using ExchangeRate.Core.Interfaces;
using ExchangeRate.Core.Interfaces.Providers;
using Microsoft.Extensions.Logging;

namespace ExchangeRate.Core.Services
{
    public class ExchangeRateOrchestrator : IExchangeRateOrchestrator
    {
        private readonly IExchangeRateRepository _repository;
        private readonly IExchangeRateCache _cache;
        private readonly IExchangeRateProviderService _providerService;
        private readonly IExchangeRateProviderFactory _providerFactory;
        private readonly ILogger<ExchangeRateOrchestrator> _logger;

        private static readonly Dictionary<string, CurrencyTypes> CurrencyMapping = 
            Enum.GetValues(typeof(CurrencyTypes)).Cast<CurrencyTypes>().ToDictionary(x => x.ToString().ToUpperInvariant());

        public ExchangeRateOrchestrator(
            IExchangeRateRepository repository,
            IExchangeRateCache cache,
            IExchangeRateProviderService providerService,
            IExchangeRateProviderFactory providerFactory,
            ILogger<ExchangeRateOrchestrator> logger)
        {
            _repository = repository;
            _cache = cache;
            _providerService = providerService;
            _providerFactory = providerFactory;
            _logger = logger;
        }

        public async Task<decimal?> GetRateAsync(string fromCurrencyCode, string toCurrencyCode, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            var fromCurrency = GetCurrencyType(fromCurrencyCode);
            var toCurrency = GetCurrencyType(toCurrencyCode);

            return await GetRateAsync(fromCurrency, toCurrency, date, source, frequency);
        }

        private async Task<decimal?> GetRateAsync(CurrencyTypes fromCurrency, CurrencyTypes toCurrency, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            if (fromCurrency == toCurrency) return 1m;

            date = date.Date;

            try 
            {
                var provider = _providerFactory.GetExchangeRateProvider(source);
                var providerCurrency = provider.Currency;

                // 1. Check Pegged currencies FIRST before potentially expensive provider lookups
                var peggedRate = await GetPeggedRateAsync(fromCurrency, toCurrency, date, source, frequency, provider);
                if (peggedRate.HasValue) return peggedRate.Value;
                
                // 2. If neither currency is the provider currency, it's a cross-rate via the provider currency.
                if (fromCurrency != providerCurrency && toCurrency != providerCurrency)
                {
                    // Calculate Cross Rate: From -> Base -> To
                    var fromToBase = await GetRateAsync(fromCurrency, providerCurrency, date, source, frequency);
                    var baseToTo = await GetRateAsync(providerCurrency, toCurrency, date, source, frequency);

                    if (fromToBase.HasValue && baseToTo.HasValue)
                    {
                        return fromToBase.Value * baseToTo.Value;
                    }
                    
                    return null;
                }

                // 3. If one of them IS the provider currency, we have a direct lookup.
                CurrencyTypes lookupCurrency;
                if (toCurrency == providerCurrency)
                {
                    lookupCurrency = fromCurrency;
                }
                else
                {
                    lookupCurrency = toCurrency;
                }
                
                // Find the raw rate for the lookup currency
                var fxRate = await FindFxRateAsync(lookupCurrency, date, source, frequency);

                if (fxRate.HasValue)
                {
                     // QuoteType Logic
                     /*
                       If your local currency is EUR (Base):
                       - Direct exchange rate: 1 USD = 0.92819 EUR
                       - Indirect exchange rate: 1 EUR = 1.08238 USD
                    */
                    
                    return provider.QuoteType switch
                    {
                        QuoteTypes.Direct when toCurrency == provider.Currency => fxRate.Value, // From(Lookup) -> To(Base). 1 Lookup = X Base. Result X.
                        QuoteTypes.Direct when fromCurrency == provider.Currency => 1m / fxRate.Value, // From(Base) -> To(Lookup). 1 Base = (1/X) Lookup.
                        
                        QuoteTypes.Indirect when fromCurrency == provider.Currency => fxRate.Value, // From(Base) -> To(Lookup). 1 Base = X Lookup. Result X.
                        QuoteTypes.Indirect when toCurrency == provider.Currency => 1m / fxRate.Value, // From(Lookup) -> To(Base). 1 Lookup = (1/X) Base.
                        
                        _ => throw new InvalidOperationException("Unsupported QuoteType")
                    };
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rate for {from} to {to} on {date}", fromCurrency, toCurrency, date);
                return null;
            }
        }

        private async Task<decimal?> FindFxRateAsync(CurrencyTypes lookupCurrency, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            // Check Cache first for EXACT date
             var cachedExact = _cache.TryGetRate(lookupCurrency, date, source, frequency);
             if (cachedExact.HasValue) return cachedExact.Value;
             
             // Check DB for EXACT date
             var dbRateExact = await _repository.GetRateAsync(lookupCurrency, date, source, frequency);
             if (dbRateExact != null)
             {
                 _cache.SetRate(dbRateExact);
                 return dbRateExact.Rate;
             }

             // FETCH from External Provider (Requested Month)
             var newRates = _providerService.FetchRates(source, frequency, date).ToList();
             if (newRates.Any())
             {
                 await _repository.SaveRatesAsync(newRates);
                 _cache.SetRates(newRates);
             }

             // Special handling for Future Dates or gaps:
             // If the requested date is in the future, we might not have found it in the "Future" fetch (which implies empty).
             // We should ensure we have the *Current* month's data loaded, so we can fall back to the latest available rate (Today/Yesterday).
             // Only do this if we haven't already fetched the current month (i.e. if date was already current month, we did it above).
             var today = DateTime.UtcNow.Date;
             if (date > today && (date.Month != today.Month || date.Year != today.Year))
             {
                 var currentRates = _providerService.FetchRates(source, frequency, today).ToList();
                 if (currentRates.Any())
                 {
                     await _repository.SaveRatesAsync(currentRates);
                     _cache.SetRates(currentRates);
                 }
             }

             // Lookback Logic
             // Loop backwards to find the most recent valid rate.
             // We use _cache.GetMinDate() as the floor if available, to avoid arbitrary stops if we have data.
             // If no minDate, we use a reasonable heuristic (e.g. 45 days) to avoid infinite loops on empty systems.
             
             var minDate = _cache.GetMinDate(source, frequency);
             var defaultLookback = frequency == ExchangeRateFrequencies.Monthly ? 45 : 15;
             var stopDate = minDate ?? date.AddDays(-defaultLookback);

             // If date is huge future, and minDate is today, we want to loop date -> minDate.
             // Ensure stopDate is not after date (can happen if minDate is future? unlikely).
             if (stopDate > date) stopDate = date;

            for (var d = date; d >= stopDate; d = d.AddDays(-1))
            {
                // Check Cache
                var cached = _cache.TryGetRate(lookupCurrency, d, source, frequency);
                if (cached.HasValue) return cached.Value;
                
                // Check DB
                var dbRate = await _repository.GetRateAsync(lookupCurrency, d, source, frequency);
                if (dbRate != null)
                {
                    _cache.SetRate(dbRate);
                    return dbRate.Rate;
                }
            }
            
            return null;
        }

        private async Task<decimal?> GetPeggedRateAsync(CurrencyTypes fromCurrency, CurrencyTypes toCurrency, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency, IExchangeRateProvider provider)
        {
             var pegged = _repository.GetPeggedCurrencies().ToDictionary(x => x.CurrencyId!.Value);
            
            // Case 1: FromCurrency is Pegged
            if (pegged.TryGetValue(fromCurrency, out var peggedFrom))
            {
                // From -> PegBase -> To
                var rateToPeg = peggedFrom.Rate!.Value;
                var pegBaseToTo = await GetRateAsync(peggedFrom.PeggedTo!.Value, toCurrency, date, source, frequency);
                if (pegBaseToTo.HasValue)
                    return rateToPeg * pegBaseToTo.Value;
            }

            // Case 2: ToCurrency is Pegged
            if (pegged.TryGetValue(toCurrency, out var peggedTo))
            {
                // From -> To
                // From -> PegBase -> To
                var fromToPegBase = await GetRateAsync(fromCurrency, peggedTo.PeggedTo!.Value, date, source, frequency);
                if (fromToPegBase.HasValue)
                    return fromToPegBase.Value / peggedTo.Rate!.Value;
            }

            return null;
        }

        private static CurrencyTypes GetCurrencyType(string currencyCode)
        {
             if (string.IsNullOrWhiteSpace(currencyCode))
                throw new ExchangeRateException("Null or empty currency code.");

            if (!CurrencyMapping.TryGetValue(currencyCode.ToUpperInvariant(), out var currency))
                throw new ExchangeRateException("Not supported currency code: " + currencyCode);

            return currency;
        }
    }
}
