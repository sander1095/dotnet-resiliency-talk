using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace ResiliencyDemo.Web;

public class WeatherApiClient(HttpClient httpClient, ILogger<WeatherApiClient> logger, RetryTrackingService retryTracker, CircuitBreakerDemoState circuitState)
{
    // Basic retry policy with exponential backoff and jitter
    private readonly ResiliencePipeline<WeatherForecast[]> _retryPipeline = new ResiliencePipelineBuilder<WeatherForecast[]>()
        .AddRetry(new RetryStrategyOptions<WeatherForecast[]>
        {
            ShouldHandle = new PredicateBuilder<WeatherForecast[]>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .Handle<TimeoutRejectedException>(),
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            OnRetry = args =>
            {
                retryTracker.LogEvent("RETRY", "🔄 Retry Pattern", $"Attempt {args.AttemptNumber + 1} for /unreliable-weather (Delay: {args.RetryDelay.TotalMilliseconds:F0}ms)");
                Console.WriteLine($"🔄 RETRY: Attempt {args.AttemptNumber + 1} for endpoint '/unreliable-weather' (Delay: {args.RetryDelay.TotalMilliseconds:F0}ms)");
                return ValueTask.CompletedTask;
            }
        })
        .Build();

    // Circuit breaker policy for unreliable endpoint - NO RETRY, just circuit breaker
    private readonly ResiliencePipeline<WeatherForecast[]> _circuitBreakerPipeline = new ResiliencePipelineBuilder<WeatherForecast[]>()
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions<WeatherForecast[]>
        {
            ShouldHandle = new PredicateBuilder<WeatherForecast[]>()
                .Handle<HttpRequestException>()
                .Handle<InvalidOperationException>()
                .Handle<TaskCanceledException>(),
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(10),
            MinimumThroughput = 3,
            BreakDuration = TimeSpan.FromSeconds(5),
            OnOpened = args =>
            {
                circuitState.OnCircuitOpened();
                retryTracker.LogEvent("CIRCUIT_OPEN", "⚡ Circuit Breaker", $"Circuit opened! Will remain open for {args.BreakDuration.TotalSeconds} seconds");
                Console.WriteLine($"🚨 CIRCUIT BREAKER OPENED: Will remain open for {args.BreakDuration.TotalSeconds} seconds");
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                circuitState.OnCircuitClosed();
                retryTracker.LogEvent("CIRCUIT_CLOSED", "⚡ Circuit Breaker", "Circuit closed - normal operation resumed");
                Console.WriteLine("✅ CIRCUIT BREAKER CLOSED: Normal operation resumed");
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = args =>
            {
                circuitState.OnCircuitHalfOpened();
                retryTracker.LogEvent("CIRCUIT_HALF_OPEN", "⚡ Circuit Breaker", "Circuit half-open - testing if service has recovered");
                Console.WriteLine("🔍 CIRCUIT BREAKER HALF-OPEN: Testing if service has recovered");
                return ValueTask.CompletedTask;
            }
        })
        .Build();

    // Timeout policy for slow endpoint
    private readonly ResiliencePipeline<WeatherForecast[]> _timeoutPipeline = new ResiliencePipelineBuilder<WeatherForecast[]>()
        .AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(2), // Reduced from 3s to 2s for more predictable demo
            OnTimeout = args =>
            {
                retryTracker.LogEvent("TIMEOUT", "⏰ Timeout Pattern", $"Request exceeded {args.Timeout.TotalSeconds} seconds and was cancelled");
                Console.WriteLine($"⏰ TIMEOUT: Request exceeded {args.Timeout.TotalSeconds} seconds and was cancelled");
                return ValueTask.CompletedTask;
            }
        })
        .Build();

    public async Task<WeatherForecast[]> GetWeatherAsync(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("📡 Calling /weatherforecast with BASIC endpoint (no special policies)");

        return await _retryPipeline.ExecuteAsync(async _ =>
        {
            var response = await httpClient.GetFromJsonAsync<WeatherForecast[]>("/weatherforecast", cancellationToken);
            return response ?? [];
        }, cancellationToken);
    }

    public async Task<WeatherForecast[]> GetWeatherWithRetryAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("🔄 Calling /unreliable-weather with RETRY policy (exponential backoff + jitter)");

        try
        {
            var result = await _retryPipeline.ExecuteAsync(async _ =>
            {
                var response = await httpClient.GetFromJsonAsync<WeatherForecast[]>("/unreliable-weather", cancellationToken);
                return response ?? [];
            }, cancellationToken);

            retryTracker.LogEvent("SUCCESS", "🔄 Retry Pattern", $"Successfully retrieved {result.Length} weather forecasts");
            return result;
        }
        catch (Exception ex)
        {
            retryTracker.LogEvent("FAILURE", "🔄 Retry Pattern", "Request failed after all retries", ex.Message);
            throw;
        }
    }

    public async Task<WeatherForecast[]> GetUnreliableWeatherAsync(CancellationToken cancellationToken = default)
    {
        // Track that we're making an attempt (this advances the demo step)
        circuitState.OnRequestAttempt();

        var demoBehavior = circuitState.GetDemoBehavior();
        var behaviorDescription = circuitState.GetBehaviorDescription();

        logger.LogInformation("⚡ Calling /unreliable-weather with CIRCUIT BREAKER policy. Demo step {DemoStep}: {BehaviorDescription}",
            circuitState.CurrentStep, behaviorDescription);

        try
        {
            var result = await _circuitBreakerPipeline.ExecuteAsync(async _ =>
            {
                var url = $"/unreliable-weather?demo={demoBehavior}";
                var response = await httpClient.GetFromJsonAsync<WeatherForecast[]>(url, cancellationToken);
                return response ?? [];
            }, cancellationToken);

            retryTracker.LogEvent("SUCCESS", "⚡ Circuit Breaker", $"Step {circuitState.CurrentStep}: Successfully retrieved {result.Length} weather forecasts");
            return result;
        }
        catch (Exception ex)
        {
            retryTracker.LogEvent("FAILURE", "⚡ Circuit Breaker", $"Step {circuitState.CurrentStep}: Request failed - {behaviorDescription}", ex.Message);
            throw;
        }
    }

    public async Task<WeatherForecast[]> GetCircuitBreakerWeatherAsync(CancellationToken cancellationToken = default)
    {
        // Track that we're making an attempt (this advances the demo step)
        circuitState.OnRequestAttempt();

        var demoBehavior = circuitState.GetDemoBehavior();
        var behaviorDescription = circuitState.GetBehaviorDescription();

        logger.LogInformation("⚡ Calling /circuit-breaker-weather with CIRCUIT BREAKER policy. Demo step {DemoStep}: {BehaviorDescription}",
            circuitState.CurrentStep, behaviorDescription);

        try
        {
            var result = await _circuitBreakerPipeline.ExecuteAsync(async _ =>
            {
                // Use dedicated /circuit-breaker-weather endpoint
                var url = $"/circuit-breaker-weather?demo={demoBehavior}";
                var response = await httpClient.GetFromJsonAsync<WeatherForecast[]>(url, cancellationToken);
                return response ?? [];
            }, cancellationToken);

            retryTracker.LogEvent("SUCCESS", "⚡ Circuit Breaker", $"Step {circuitState.CurrentStep}: Successfully retrieved {result.Length} weather forecasts");
            return result;
        }
        catch (Exception ex)
        {
            retryTracker.LogEvent("FAILURE", "⚡ Circuit Breaker", $"Step {circuitState.CurrentStep}: Request failed - {behaviorDescription}", ex.Message);
            throw;
        }
    }

    public async Task<WeatherForecast[]> GetSlowWeatherAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("🐌 Calling /slow-weather with TIMEOUT policy (2 second timeout)");

        try
        {
            var result = await _timeoutPipeline.ExecuteAsync(async timeoutToken =>
            {
                // Use the timeout token from Polly, not the original cancellation token
                var response = await httpClient.GetFromJsonAsync<WeatherForecast[]>("/slow-weather", timeoutToken);
                return response ?? [];
            }, cancellationToken);

            retryTracker.LogEvent("SUCCESS", "⏰ Timeout Pattern", $"Successfully retrieved {result.Length} weather forecasts within timeout");
            return result;
        }
        catch (TimeoutRejectedException ex)
        {
            retryTracker.LogEvent("TIMEOUT", "⏰ Timeout Pattern", "Request exceeded 2 seconds and was cancelled", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            retryTracker.LogEvent("FAILURE", "⏰ Timeout Pattern", "Request failed due to other error", ex.Message);
            throw;
        }
    }

    public async Task<WeatherForecast[]> GetRateLimitedWeatherAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("🚦 Calling /rate-limited-weather with simple HTTP client (rate limiting handled by server)");

        try
        {
            var response = await httpClient.GetFromJsonAsync<WeatherForecast[]>("/rate-limited-weather", cancellationToken);
            var result = response ?? [];
            retryTracker.LogEvent("SUCCESS", "🚦 Rate Limiting", $"Successfully retrieved {result.Length} weather forecasts from rate-limited endpoint");
            return result;
        }
        catch (Exception ex)
        {
            retryTracker.LogEvent("FAILURE", "🚦 Rate Limiting", "Request was rate limited or failed", ex.Message);
            throw;
        }
    }
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
