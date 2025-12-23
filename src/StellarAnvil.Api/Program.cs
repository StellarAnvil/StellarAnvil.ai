using System.Net.Http.Headers;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register HttpClient for Ollama proxy
builder.Services.AddHttpClient("Ollama", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"]!);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// GET /v1/models - proxy to Ollama
app.MapGet("/v1/models", async (IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("Ollama");
    var response = await client.GetStringAsync("/v1/models");
    return Results.Content(response, "application/json");
})
.WithName("ListModels");

// POST /v1/chat/completions - proxy to Ollama with streaming support
app.MapPost("/v1/chat/completions", async (HttpContext context, IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("Ollama");

    // Forward request to Ollama
    var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
    {
        Content = new StreamContent(context.Request.Body)
    };
    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

    // Copy response headers
    context.Response.StatusCode = (int)response.StatusCode;
    context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";

    // Stream response body directly
    await response.Content.CopyToAsync(context.Response.Body);
})
.WithName("CreateChatCompletion");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
