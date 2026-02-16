using ExchangeRate.Api.Infrastructure;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Infrastructure;
using ExchangeRate.Core.Interfaces;
using ExchangeRate.Core.Interfaces.Providers;
using ExchangeRate.Core.Models;
using ExchangeRate.Core.Providers;
using ExchangeRate.Core.Services;

using ExchangeRate.Api.Endpoints;

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

app.MapExchangeRateEndpoints();

app.Run();

// Make Program accessible to test project
public partial class Program { }

