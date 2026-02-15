using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExchangeRate.Core.Entities;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Interfaces;
using ExchangeRate.Core.Infrastructure;

namespace ExchangeRate.Core
{
    public class ExchangeRateRepository : IExchangeRateRepository
    {
        private readonly IExchangeRateDataStore _dataStore;

        public ExchangeRateRepository(IExchangeRateDataStore dataStore)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        }

        public async Task<Entities.ExchangeRate?> GetRateAsync(CurrencyTypes currency, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            // We need to fetch rates for the day. Since DataStore returns a list for a range, we ask for [date, date+1)
            var rates = await _dataStore.GetExchangeRatesAsync(date.Date, date.Date.AddDays(1));
            
            // In a real DB scenario, we would filter in the query. DataStore is in-memory list so filtering happens there or here.
            // Depending on DataStore implementation, we might need to filter more specifically.
            foreach (var rate in rates)
            {
                if (rate.CurrencyId == currency && 
                    rate.Source == source && 
                    rate.Frequency == frequency &&
                    rate.Date?.Date == date.Date)
                {
                    return rate;
                }
            }

            return null;
        }

        public async Task SaveRatesAsync(IEnumerable<Entities.ExchangeRate> rates)
        {
            await _dataStore.SaveExchangeRatesAsync(rates);
        }

        public IEnumerable<PeggedCurrency> GetPeggedCurrencies()
        {
            return _dataStore.GetPeggedCurrencies();
        }
    }
}
