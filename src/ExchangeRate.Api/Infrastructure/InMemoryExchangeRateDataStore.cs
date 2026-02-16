using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExchangeRate.Core.Interfaces;
using ExchangeRate.Core.Infrastructure;

namespace ExchangeRate.Api.Infrastructure
{
    /// <summary>
    /// In-memory implementation of IExchangeRateDataStore.
    /// Thread-safe and supports Upserts.
    /// </summary>
    public class InMemoryExchangeRateDataStore : IExchangeRateDataStore
    {
        // Using ConcurrentBag or ConcurrentDictionary. 
        // For search performance, a List with a ReaderWriterLockSlim might be better, 
        // but given the "Prototype" nature, a simple lock or ConcurrentBag is fine.
        // Let's use a standard List with locking to ensure consistency during range queries.
        
        private readonly List<Core.Entities.ExchangeRate> _exchangeRates = new();
        private readonly List<Core.Entities.PeggedCurrency> _peggedCurrencies = new();
        private readonly object _lock = new();

        public IQueryable<Core.Entities.ExchangeRate> ExchangeRates
        {
            get
            {
                lock (_lock)
                {
                    // Return a snapshot/copy to avoid enumeration issues
                    return _exchangeRates.ToList().AsQueryable();
                }
            }
        }

        public Task<List<Core.Entities.ExchangeRate>> GetExchangeRatesAsync(DateTime minDate, DateTime maxDate)
        {
            lock (_lock)
            {
                var rates = _exchangeRates
                    .Where(r => r.Date.HasValue && r.Date.Value >= minDate && r.Date.Value < maxDate)
                    .ToList();
                return Task.FromResult(rates);
            }
        }

        public Task SaveExchangeRatesAsync(IEnumerable<Core.Entities.ExchangeRate> rates)
        {
            lock (_lock)
            {
                foreach (var rate in rates)
                {
                    // Upsert Logic: Remove existing if matches, then add new.
                    // Ideally we'd use a Dictionary for O(1) lookups if we had a primary key
                    // But here we identify by (Date, Currency, Source, Frequency)
                    
                    var existingIndex = _exchangeRates.FindIndex(r =>
                        r.Date == rate.Date &&
                        r.CurrencyId == rate.CurrencyId &&
                        r.Source == rate.Source &&
                        r.Frequency == rate.Frequency);

                    if (existingIndex >= 0)
                    {
                        // Replace
                        _exchangeRates[existingIndex] = rate;
                    }
                    else
                    {
                        // Add
                        _exchangeRates.Add(rate);
                    }
                }
            }
            return Task.CompletedTask;
        }

        public List<Core.Entities.PeggedCurrency> GetPeggedCurrencies()
        {
            lock (_lock)
            {
                return _peggedCurrencies.ToList();
            }
        }

        public void AddPeggedCurrency(Core.Entities.PeggedCurrency peggedCurrency)
        {
            lock (_lock)
            {
                _peggedCurrencies.Add(peggedCurrency);
            }
        }
    }
}
