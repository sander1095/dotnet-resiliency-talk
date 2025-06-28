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
app.MapGet("/unreliable-weather", (ILogger<Program> logger, string? demo = null) =>
{
    var currentRequest = Interlocked.Increment(ref requestCounter);
    logger.LogInformation("⚠️  Unreliable Weather API: Processing request #{RequestNumber} with demo behavior: {DemoBehavior}", currentRequest, demo ?? "normal");

    // Determine failure behavior based on demo parameter
    bool shouldFail = demo switch
    {
        "force-fail" => true,      // Always fail (for circuit breaker OPEN state)
        "force-success" => false,  // Always succeed (for circuit breaker HALF-OPEN state)
        _ => Random.Shared.NextDouble() < 0.6  // Normal 60% failure rate
    };

    if (shouldFail)
    {
        var reason = demo switch
        {
            "force-fail" => "Circuit breaker is OPEN - failing fast",
            _ => "Service temporarily unavailable - simulated failure"
        };

        logger.LogError("💥 Unreliable Weather API: FAILED request #{RequestNumber} - {Reason}", currentRequest, reason);
        throw new InvalidOperationException(reason);
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
    var delay = Random.Shared.Next(500, 2300); // 1s to 6s (more predictable timeout demo)
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

// Dedicated endpoint for circuit breaker demo (separate from retry pattern)
app.MapGet("/circuit-breaker-weather", (ILogger<Program> logger, string? demo = null) =>
{
    var currentRequest = Interlocked.Increment(ref requestCounter);
    logger.LogInformation("⚡ Circuit Breaker Weather API: Processing request #{RequestNumber} with demo behavior: {DemoBehavior}", currentRequest, demo ?? "normal");

    // Determine failure behavior based on demo parameter
    bool shouldFail = demo switch
    {
        "force-fail" => true,      // Always fail (for circuit breaker demo)
        "force-success" => false,  // Always succeed (for circuit breaker demo)
        _ => Random.Shared.NextDouble() < 0.7  // Default 70% failure rate for circuit breaker
    };

    if (shouldFail)
    {
        var reason = demo switch
        {
            "force-fail" => "Circuit breaker demo - forced failure",
            _ => "Circuit breaker service temporarily unavailable"
        };

        logger.LogError("💥 Circuit Breaker Weather API: FAILED request #{RequestNumber} - {Reason}", currentRequest, reason);
        throw new InvalidOperationException(reason);
    }

    var forecast = Enumerable.Range(1, 3).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    logger.LogInformation("✅ Circuit Breaker Weather API: Successfully returned forecast for request #{RequestNumber}", currentRequest);
    return forecast;
})
.WithName("GetCircuitBreakerWeatherForecast");

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
