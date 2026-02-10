using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using PokeScout.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Controllers for CardsController
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

//Register service in DI
builder.Services.AddSingleton<ICardService, CardService>();

// Generates /openapi/v1.json
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // /scalar
}

app.MapControllers();

app.MapGet("/", () => Results.Ok("PokeScout API is running!"));

app.Run();
