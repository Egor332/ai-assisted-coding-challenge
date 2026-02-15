using System;
using System.Collections.Generic;
using ExchangeRate.Core.Entities;
using ExchangeRate.Core.Enums;

namespace ExchangeRate.Core.Interfaces
{
    public interface IExchangeRateCache
    {
        decimal? TryGetRate(CurrencyTypes currency, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency);
        void SetRate(Entities.ExchangeRate rate);
        void SetRates(IEnumerable<Entities.ExchangeRate> rates);
        
        /// <summary>
        /// Gets the minimum date for which we have rates cached for the given source and frequency.
        /// </summary>
        DateTime? GetMinDate(ExchangeRateSources source, ExchangeRateFrequencies frequency);
        
        /// <summary>
        /// Updates the minimum date for the given source and frequency.
        /// </summary>
        void UpdateMinDate(ExchangeRateSources source, ExchangeRateFrequencies frequency, DateTime date);
    }
}
