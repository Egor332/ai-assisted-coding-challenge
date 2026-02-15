using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Interfaces;

namespace ExchangeRate.Core.Services
{
    public class ExchangeRateCache : IExchangeRateCache
    {
        // Key: (Source, Frequency) -> Value: Dictionary of (Currency -> Dictionary of (Date -> Rate))
        // Using ConcurrentDictionary for top level thread safety
        private readonly ConcurrentDictionary<(ExchangeRateSources, ExchangeRateFrequencies), ConcurrentDictionary<CurrencyTypes, ConcurrentDictionary<DateTime, decimal>>> _cache 
            = new();

        // Tracks the minimum date processed/cached for optimization
        private readonly ConcurrentDictionary<(ExchangeRateSources, ExchangeRateFrequencies), DateTime> _minFxDates 
            = new();

        public decimal? TryGetRate(CurrencyTypes currency, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            if (_cache.TryGetValue((source, frequency), out var currencyDict) &&
                currencyDict.TryGetValue(currency, out var dateDict) &&
                dateDict.TryGetValue(date.Date, out var rate))
            {
                return rate;
            }

            return null;
        }

        public void SetRate(Entities.ExchangeRate rate)
        {
            if (rate.Date == null || rate.Rate == null || rate.Source == null || rate.Frequency == null || rate.CurrencyId == null)
                return;

            var sourceFreqKey = (rate.Source.Value, rate.Frequency.Value);
            
            var currencyDict = _cache.GetOrAdd(sourceFreqKey, _ => new ConcurrentDictionary<CurrencyTypes, ConcurrentDictionary<DateTime, decimal>>());
            var dateDict = currencyDict.GetOrAdd(rate.CurrencyId.Value, _ => new ConcurrentDictionary<DateTime, decimal>());
            
            // Upsert: Overwrite if exists (Last write wins)
            dateDict[rate.Date.Value.Date] = rate.Rate.Value;
        }

        public void SetRates(IEnumerable<Entities.ExchangeRate> rates)
        {
            foreach (var rate in rates)
            {
                SetRate(rate);
            }
        }

        public DateTime? GetMinDate(ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            if (_minFxDates.TryGetValue((source, frequency), out var date))
            {
                return date;
            }
            return null;
        }

        public void UpdateMinDate(ExchangeRateSources source, ExchangeRateFrequencies frequency, DateTime date)
        {
            _minFxDates.AddOrUpdate((source, frequency), date, (existingKey, existingDate) => date < existingDate ? date : existingDate);
        }
    }
}
