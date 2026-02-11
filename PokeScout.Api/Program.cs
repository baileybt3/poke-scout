using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using PokeScout.Api.Services;
using Microsoft.EntityFrameworkCore;
using PokeScout.Api.Data;

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

// Generates /openapi/v1.json
builder.Services.AddOpenApi();

// Register DbContext
builder.Services.AddDbContext<PokeScoutDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // /scalar
}

app.MapControllers();

app.MapGet("/", () => Results.Ok("PokeScout API is running!"));

app.Run();
