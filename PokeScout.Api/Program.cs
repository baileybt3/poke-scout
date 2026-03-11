using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PokeScout.Api.Data;
using PokeScout.Api.Services;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Controllers for CardsController
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

//Register service in DI
builder.Services.AddScoped<ICardService, EfCardService>();

// API Service registration
builder.Services.AddHttpClient<IPokemonCatalogService, PokemonCatalogService>(client =>
{
    client.BaseAddress = new Uri("https://api.tcgdex.net/v2/en/");
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // /scalar
    app.UseCors("WebDev");
    app.UseCors("DevCors");
}

app.MapControllers();

app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok("PokeScout API is running!"));

app.Run();
