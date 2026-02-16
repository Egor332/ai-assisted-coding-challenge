using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ExchangeRate.Api.Endpoints
{
    public static class ExchangeRateEndpoints
    {
        public static void MapExchangeRateEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/rates", GetExchangeRate)
                .WithName("GetExchangeRate");
        }

        private static async Task<IResult> GetExchangeRate(
            string from,
            string to,
            DateTime date,
            ExchangeRateSources source,
            ExchangeRateFrequencies frequency,
            IExchangeRateOrchestrator orchestrator)
        {
            try 
            {
                var rate = await orchestrator.GetRateAsync(from, to, date, source, frequency);

                if (rate == null)
                {
                    return Results.NotFound(new { error = $"No exchange rate found for {from} to {to} on {date:yyyy-MM-dd}" });
                }

                return Results.Ok(new ExchangeRateResponse(from, to, date, source.ToString(), frequency.ToString(), rate.Value));
            }
            catch (Exception ex)
            {
                // Simple global error handling for now
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        }
    }

    /// <summary>
    /// Response model for the exchange rate API endpoint.
    /// </summary>
    public record ExchangeRateResponse(
        string FromCurrency,
        string ToCurrency,
        DateTime Date,
        string Source,
        string Frequency,
        decimal Rate);
}
