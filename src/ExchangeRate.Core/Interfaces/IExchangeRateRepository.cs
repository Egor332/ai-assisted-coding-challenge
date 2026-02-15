using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExchangeRate.Core.Entities;
using ExchangeRate.Core.Enums;

namespace ExchangeRate.Core.Interfaces
{
    public interface IExchangeRateRepository
    {
        Task<Entities.ExchangeRate?> GetRateAsync(CurrencyTypes currency, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency);
        Task SaveRatesAsync(IEnumerable<Entities.ExchangeRate> rates);
        IEnumerable<PeggedCurrency> GetPeggedCurrencies();
    }
}
