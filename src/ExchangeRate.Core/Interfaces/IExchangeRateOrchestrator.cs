using System;
using System.Threading.Tasks;
using ExchangeRate.Core.Enums;

namespace ExchangeRate.Core.Interfaces
{
    public interface IExchangeRateOrchestrator
    {
        Task<decimal?> GetRateAsync(string fromCurrency, string toCurrency, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency);
    }
}
