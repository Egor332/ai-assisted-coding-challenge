using ExchangeRate.Api.Infrastructure;
using ExchangeRate.Core;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Infrastructure;
using ExchangeRate.Core.Interfaces;
using ExchangeRate.Core.Interfaces.Providers;
using ExchangeRate.Core.Models;
using ExchangeRate.Core.Providers;
using ExchangeRate.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure ExchangeRate services
builder.Services.AddSingleton<ExternalExchangeRateApiConfig>(sp =>
{
    var config = builder.Configuration.GetSection("ExchangeRateApi").Get<ExternalExchangeRateApiConfig>();
    return config ?? new ExternalExchangeRateApiConfig
    {
        BaseAddress = builder.Configuration["ExchangeRateApi:BaseAddress"] ?? "http://localhost",
        TokenEndpoint = builder.Configuration["ExchangeRateApi:TokenEndpoint"] ?? "/connect/token",
        ClientId = builder.Configuration["ExchangeRateApi:ClientId"] ?? "client",
        ClientSecret = builder.Configuration["ExchangeRateApi:ClientSecret"] ?? "secret"
    };
});

// Register HttpClient for providers
builder.Services.AddHttpClient<EUECBExchangeRateProvider>();
builder.Services.AddHttpClient<MXCBExchangeRateProvider>();

// Register Providers
builder.Services.AddSingleton<IExchangeRateProvider, EUECBExchangeRateProvider>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(EUECBExchangeRateProvider));
    var config = sp.GetRequiredService<ExternalExchangeRateApiConfig>();
    return new EUECBExchangeRateProvider(httpClient, config);
});

builder.Services.AddSingleton<IExchangeRateProvider, MXCBExchangeRateProvider>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(MXCBExchangeRateProvider));
    var config = sp.GetRequiredService<ExternalExchangeRateApiConfig>();
    return new MXCBExchangeRateProvider(httpClient, config);
});

// Architecture Components
builder.Services.AddSingleton<IExchangeRateProviderFactory, ExchangeRateProviderFactory>();
builder.Services.AddSingleton<IExchangeRateCache, ExchangeRateCache>();
builder.Services.AddSingleton<IExchangeRateDataStore, InMemoryExchangeRateDataStore>();
builder.Services.AddSingleton<IExchangeRateRepository, ExchangeRateRepository>();
builder.Services.AddSingleton<IExchangeRateProviderService, ExchangeRateProviderService>();
builder.Services.AddSingleton<IExchangeRateOrchestrator, ExchangeRateOrchestrator>();


var app = builder.Build();

// GET /api/rates?from={currency}&to={currency}&date={date}&source={source}&frequency={frequency}
app.MapGet("/api/rates", async (
    string from,
    string to,
    DateTime date,
    ExchangeRateSources source,
    ExchangeRateFrequencies frequency,
    IExchangeRateOrchestrator orchestrator) =>
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
});

app.Run();

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

// Make Program accessible to test project
public partial class Program { }

