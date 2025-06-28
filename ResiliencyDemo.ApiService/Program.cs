using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

// Counter to simulate various failure scenarios
var requestCounter = 0;

app.MapGet("/weatherforecast", (ILogger<Program> logger) =>
{
    var currentRequest = Interlocked.Increment(ref requestCounter);
    logger.LogInformation("🌤️  Weather API: Processing request #{RequestNumber}", currentRequest);

    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    logger.LogInformation("✅ Weather API: Successfully returned forecast for request #{RequestNumber}", currentRequest);
    return forecast;
})
.WithName("GetWeatherForecast");

// Endpoint that fails randomly (for circuit breaker demo)
app.MapGet("/unreliable-weather", (ILogger<Program> logger) =>
{
    var currentRequest = Interlocked.Increment(ref requestCounter);
    logger.LogInformation("⚠️  Unreliable Weather API: Processing request #{RequestNumber}", currentRequest);

    // Fail 60% of the time to demonstrate circuit breaker
    if (Random.Shared.NextDouble() < 0.6)
    {
        logger.LogError("💥 Unreliable Weather API: FAILED request #{RequestNumber} - Service temporarily unavailable", currentRequest);
        throw new InvalidOperationException("Service temporarily unavailable - simulated failure");
    }

    var forecast = Enumerable.Range(1, 3).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    logger.LogInformation("✅ Unreliable Weather API: Successfully returned forecast for request #{RequestNumber}", currentRequest);
    return forecast;
})
.WithName("GetUnreliableWeatherForecast");

// Endpoint that times out occasionally (for timeout demo)
app.MapGet("/slow-weather", async (ILogger<Program> logger) =>
{
    var currentRequest = Interlocked.Increment(ref requestCounter);
    logger.LogInformation("🐌 Slow Weather API: Processing request #{RequestNumber}", currentRequest);

    // Randomly introduce delays to demonstrate timeouts
    var delay = Random.Shared.Next(100, 8000); // 0.1s to 8s
    logger.LogInformation("⏱️  Slow Weather API: Simulating {DelayMs}ms processing time for request #{RequestNumber}", delay, currentRequest);

    await Task.Delay(delay);

    var forecast = Enumerable.Range(1, 2).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    logger.LogInformation("✅ Slow Weather API: Successfully returned forecast for request #{RequestNumber} after {DelayMs}ms", currentRequest, delay);
    return forecast;
})
.WithName("GetSlowWeatherForecast");

// Rate limited endpoint (for rate limiting demo)
app.MapGet("/rate-limited-weather", (ILogger<Program> logger) =>
{
    var currentRequest = Interlocked.Increment(ref requestCounter);
    logger.LogInformation("🚦 Rate Limited Weather API: Processing request #{RequestNumber}", currentRequest);

    var forecast = Enumerable.Range(1, 1).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    logger.LogInformation("✅ Rate Limited Weather API: Successfully returned forecast for request #{RequestNumber}", currentRequest);
    return forecast;
})
.WithName("GetRateLimitedWeatherForecast");

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
