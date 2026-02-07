using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Generates /openapi/v1.json
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // /scalar
}

app.MapGet("/", () => Results.Ok("PokeScout API is running!"));

app.Run();
