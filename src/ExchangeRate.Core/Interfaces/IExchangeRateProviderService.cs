using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExchangeRate.Core.Entities;
using ExchangeRate.Core.Enums;

namespace ExchangeRate.Core.Interfaces
{
    public interface IExchangeRateProviderService
    {
        /// <summary>
        /// Fetches rates from external provider. Optimized to fetch a while month if possible to reduce API calls.
        /// </summary>
        IEnumerable<Entities.ExchangeRate> FetchRates(ExchangeRateSources source, ExchangeRateFrequencies frequency, DateTime date);
    }
}
