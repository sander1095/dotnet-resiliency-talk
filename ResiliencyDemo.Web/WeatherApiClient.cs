using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace ResiliencyDemo.Web;

public class WeatherApiClient(HttpClient httpClient, ILogger<WeatherApiClient> logger)
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
                Console.WriteLine($"🔄 RETRY: Attempt {args.AttemptNumber + 1} for endpoint '/weatherforecast' (Delay: {args.RetryDelay.TotalMilliseconds:F0}ms)");
                return ValueTask.CompletedTask;
            }
        })
        .Build();

    // Circuit breaker policy for unreliable endpoint
    private readonly ResiliencePipeline<WeatherForecast[]> _circuitBreakerPipeline = new ResiliencePipelineBuilder<WeatherForecast[]>()
        .AddRetry(new RetryStrategyOptions<WeatherForecast[]>
        {
            ShouldHandle = new PredicateBuilder<WeatherForecast[]>()
                .Handle<HttpRequestException>()
                .Handle<InvalidOperationException>()
                .Handle<TaskCanceledException>(),
            MaxRetryAttempts = 2,
            Delay = TimeSpan.FromMilliseconds(500),
            OnRetry = args =>
            {
                Console.WriteLine($"🔄 CIRCUIT BREAKER RETRY: Attempt {args.AttemptNumber + 1} for unreliable endpoint");
                return ValueTask.CompletedTask;
            }
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions<WeatherForecast[]>
        {
            ShouldHandle = new PredicateBuilder<WeatherForecast[]>()
                .Handle<HttpRequestException>()
                .Handle<InvalidOperationException>()
                .Handle<TaskCanceledException>(),
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(10),
            MinimumThroughput = 3,
            BreakDuration = TimeSpan.FromSeconds(10),
            OnOpened = args =>
            {
                Console.WriteLine($"🚨 CIRCUIT BREAKER OPENED: Will remain open for {args.BreakDuration.TotalSeconds} seconds");
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                Console.WriteLine("✅ CIRCUIT BREAKER CLOSED: Normal operation resumed");
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = args =>
            {
                Console.WriteLine("🔍 CIRCUIT BREAKER HALF-OPEN: Testing if service has recovered");
                return ValueTask.CompletedTask;
            }
        })
        .Build();

    // Timeout policy for slow endpoint
    private readonly ResiliencePipeline<WeatherForecast[]> _timeoutPipeline = new ResiliencePipelineBuilder<WeatherForecast[]>()
        .AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(3),
            OnTimeout = args =>
            {
                Console.WriteLine($"⏰ TIMEOUT: Request exceeded {args.Timeout.TotalSeconds} seconds and was cancelled");
                return ValueTask.CompletedTask;
            }
        })
        .AddRetry(new RetryStrategyOptions<WeatherForecast[]>
        {
            ShouldHandle = new PredicateBuilder<WeatherForecast[]>()
                .Handle<TimeoutRejectedException>()
                .Handle<TaskCanceledException>(),
            MaxRetryAttempts = 2,
            Delay = TimeSpan.FromSeconds(1),
            OnRetry = args =>
            {
                Console.WriteLine($"🔄 TIMEOUT RETRY: Attempt {args.AttemptNumber + 1} after timeout");
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
        logger.LogInformation("🔄 Calling /weatherforecast with RETRY policy (exponential backoff + jitter)");

        return await _retryPipeline.ExecuteAsync(async _ =>
        {
            var response = await httpClient.GetFromJsonAsync<WeatherForecast[]>("/weatherforecast", cancellationToken);
            return response ?? [];
        }, cancellationToken);
    }

    public async Task<WeatherForecast[]> GetUnreliableWeatherAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("⚡ Calling /unreliable-weather with CIRCUIT BREAKER policy");

        return await _circuitBreakerPipeline.ExecuteAsync(async _ =>
        {
            var response = await httpClient.GetFromJsonAsync<WeatherForecast[]>("/unreliable-weather", cancellationToken);
            return response ?? [];
        }, cancellationToken);
    }

    public async Task<WeatherForecast[]> GetSlowWeatherAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("🐌 Calling /slow-weather with TIMEOUT policy (3 second timeout)");

        return await _timeoutPipeline.ExecuteAsync(async _ =>
        {
            var response = await httpClient.GetFromJsonAsync<WeatherForecast[]>("/slow-weather", cancellationToken);
            return response ?? [];
        }, cancellationToken);
    }

    public async Task<WeatherForecast[]> GetRateLimitedWeatherAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("🚦 Calling /rate-limited-weather with simple HTTP client (rate limiting handled by server)");

        var response = await httpClient.GetFromJsonAsync<WeatherForecast[]>("/rate-limited-weather", cancellationToken);
        return response ?? [];
    }
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
