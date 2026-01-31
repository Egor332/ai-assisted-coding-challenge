using System;
using System.Collections.Generic;
using System.Linq;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Interfaces;
using ExchangeRate.Core.Interfaces.Providers;

namespace ExchangeRate.Core
{
    public class ExchangeRateProviderFactory : IExchangeRateProviderFactory
    {
        private readonly Dictionary<ExchangeRateSources, IExchangeRateProvider> _providers;

        public ExchangeRateProviderFactory(IEnumerable<IExchangeRateProvider> exchangeRateProviders)
        {
            _providers = exchangeRateProviders.ToDictionary(x => x.Source);
        }

        public IExchangeRateProvider GetExchangeRateProvider(ExchangeRateSources source)
        {
            if (!_providers.TryGetValue(source, out var provider))
            {
                throw new NotSupportedException($"Source {source} is not supported.");
            }

            return provider;
        }

        public bool TryGetExchangeRateProviderByCurrency(CurrencyTypes currency, out IExchangeRateProvider provider)
        {         
            provider = _providers.Values.FirstOrDefault(x => x.Currency == currency);            
            return provider != null;
        }

        public IEnumerable<ExchangeRateSources> ListExchangeRateSources() => _providers.Keys;
    }
}
