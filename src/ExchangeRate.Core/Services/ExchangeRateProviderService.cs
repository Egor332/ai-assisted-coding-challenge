using System;
using System.Collections.Generic;
using System.Linq;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Exceptions;
using ExchangeRate.Core.Helpers;
using ExchangeRate.Core.Interfaces;
using ExchangeRate.Core.Interfaces.Providers;
using Microsoft.Extensions.Logging;

namespace ExchangeRate.Core.Services
{
    public class ExchangeRateProviderService : IExchangeRateProviderService
    {
        private readonly IExchangeRateProviderFactory _providerFactory;
        private readonly ILogger<ExchangeRateProviderService> _logger;

        public ExchangeRateProviderService(IExchangeRateProviderFactory providerFactory, ILogger<ExchangeRateProviderService> logger)
        {
            _providerFactory = providerFactory;
            _logger = logger;
        }

        public IEnumerable<Entities.ExchangeRate> FetchRates(ExchangeRateSources source, ExchangeRateFrequencies frequency, DateTime date)
        {
            try
            {
                var provider = _providerFactory.GetExchangeRateProvider(source);
                var startDate = PeriodHelper.GetStartOfMonth(date);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // Optimization: Fetch the WHOLE MONTH to minimize API calls (Rate Limiting)
                // Even if we only need one day, getting the month fills the gaps and updates the cache/db efficiently.
                
                return FetchRatesForPeriod(provider, frequency, startDate, endDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch rates for source {source}, frequency {frequency}, date {date}", source, frequency, date);
                // We return empty here to allow the system to proceed (maybe using stale data or just failing gracefully later)
                // Or rethrow if strict. Given requirements, logging and returning empty is safer for batch.
                return Enumerable.Empty<Entities.ExchangeRate>();
            }
        }

        private IEnumerable<Entities.ExchangeRate> FetchRatesForPeriod(IExchangeRateProvider provider, ExchangeRateFrequencies frequency, DateTime from, DateTime to)
        {
            switch (frequency)
            {
                case ExchangeRateFrequencies.Daily:
                    if (provider is IDailyExchangeRateProvider dailyProvider)
                        return dailyProvider.GetHistoricalDailyFxRates(from, to);
                    break;
                case ExchangeRateFrequencies.Monthly:
                    if (provider is IMonthlyExchangeRateProvider monthlyProvider)
                        return monthlyProvider.GetHistoricalMonthlyFxRates(from, to);
                    break;
                case ExchangeRateFrequencies.Weekly:
                    if (provider is IWeeklyExchangeRateProvider weeklyProvider)
                        return weeklyProvider.GetHistoricalWeeklyFxRates(from, to);
                    break;
                case ExchangeRateFrequencies.BiWeekly:
                    if (provider is IBiWeeklyExchangeRateProvider biWeeklyProvider)
                        return biWeeklyProvider.GetHistoricalBiWeeklyFxRates(from, to);
                    break;
                default:
                    throw new ExchangeRateException($"Unsupported frequency: {frequency}");
            }

            throw new ExchangeRateException($"Provider {provider.Source} does not support frequency {frequency}");
        }
    }
}
