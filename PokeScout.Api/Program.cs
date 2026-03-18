using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PokeScout.Api.Data;
using PokeScout.Api.Services;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;
using PokeScout.Api.Models;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// Controllers for CardsController
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Memory Cache Service
builder.Services.AddMemoryCache();

// Register service in DI
builder.Services.AddScoped<ICardService, EfCardService>();

// PokeWallet config
var pokeWalletBaseUrl = builder.Configuration["PokemonWallet:BaseUrl"] ?? "https://api.pokewallet.io/";
var pokeWalletApiKey = builder.Configuration["PokemonWallet:ApiKey"];

if (string.IsNullOrWhiteSpace(pokeWalletApiKey))
{
    throw new InvalidOperationException(
        "Missing PokemonWallet:ApiKey in PokeScout.Api/appsettings.Development.json");
}

// API Service registration
builder.Services.AddHttpClient<IPokemonCatalogService, PokemonCatalogService>(client =>
{
    client.BaseAddress = new Uri(pokeWalletBaseUrl);
    client.DefaultRequestHeaders.Add("X-API-Key", pokeWalletApiKey);
});

// Generates /openapi/v1.json
builder.Services.AddOpenApi();

// Service Health Check
builder.Services.AddHealthChecks();

// Register DbContext
builder.Services.AddDbContext<PokeScoutDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Service registration
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebDev", p =>
        p.WithOrigins("https://localhost:7062", "http://localhost:5062")
            .AllowAnyHeader()
            .AllowAnyMethod());

    options.AddPolicy("DevCors", p =>
        p.AllowAnyOrigin()
         .AllowAnyHeader()
         .AllowAnyMethod());
});

// Stripe registration
builder.Services.Configure<StripeOptions>(
    builder.Configuration.GetSection("Stripe"));

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseCors("DevCors");
}

app.MapControllers();

app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok("PokeScout API is running!"));

app.Run();