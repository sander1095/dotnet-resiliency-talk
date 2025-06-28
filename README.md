# 🛡️ .NET Resiliency with Polly v8 - Presentation Demo

This repository contains a comprehensive demo application showcasing resiliency patterns using **Polly v8** in a **.NET Aspire 9** application. Perfect for presentations about building resilient .NET applications!

## 🎯 What This Demo Covers

### Core Resiliency Patterns
- **🔄 Retry Pattern**: Exponential backoff with jitter for handling transient failures
- **⚡ Circuit Breaker**: Fail-fast mechanism when services are consistently unavailable  
- **⏰ Timeout Pattern**: Cancelling long-running operations to maintain responsiveness
- **🚦 Rate Limiting**: Server-side rate limiting demonstration

### Technologies Used
- **.NET 9** with ASP.NET Core
- **Polly v8** with modern resilience pipelines
- **.NET Aspire 9** for orchestration and service discovery
- **Blazor Server** for interactive UI
- **Bootstrap** for responsive design

## 🏗️ Architecture

```
┌─────────────────────┐    HTTP/gRPC    ┌──────────────────────┐
│   Web Frontend      │────────────────▶│   API Service        │
│   (Blazor Server)   │                 │   (ASP.NET Core)     │
│   - Polly Policies  │                 │   - Failure Sim      │
│   - Interactive UI  │                 │   - Multiple Endpoints│
└─────────────────────┘                 └──────────────────────┘
           │                                       │
           └─────────────────┬─────────────────────┘
                             │
                   ┌─────────▼──────────┐
                   │   .NET Aspire      │
                   │   AppHost          │
                   │   - Service Disc.  │
                   │   - Orchestration  │
                   └────────────────────┘
```

## 🚀 Getting Started

### Prerequisites
- .NET 9 SDK
- Visual Studio 2022 or VS Code
- Docker (optional, for advanced scenarios)

### Running the Demo

1. **Clone the repository**
   ```bash
   git clone <your-repo-url>
   cd dotnet-resiliency-talk
   ```

2. **Install Aspire 9 templates** (if not already installed)
   ```bash
   dotnet new install Aspire.ProjectTemplates --force
   ```

3. **Run the application**
   ```bash
   dotnet run --project ResiliencyDemo.AppHost
   ```

4. **Open your browser** and navigate to the Aspire dashboard (usually `https://localhost:17000`)

5. **Access the demo** via the Web Frontend URL shown in the dashboard

## 🎪 Demo Walkthrough

### For Presenters

1. **Start with the Home Page**
   - Explain the architecture and patterns
   - Show the clean Aspire dashboard

2. **Navigate to "Polly Resiliency Demo"**
   - Open browser dev tools (F12) to show console logging
   - Explain each pattern before demonstrating

3. **Retry Pattern Demo**
   - Click "Test Retry Pattern" 
   - Show console logs with retry attempts and jitter
   - Explain exponential backoff strategy

4. **Circuit Breaker Demo**
   - Click "Test Circuit Breaker" multiple times quickly
   - Show how circuit opens after failures
   - Wait 10 seconds and show it trying to close
   - Explain the three states: Closed, Open, Half-Open

5. **Timeout Pattern Demo**
   - Click "Test Timeout Pattern" multiple times
   - Show both successful (fast) and failed (timeout) responses
   - Explain timeout + retry combination

6. **Rate Limiting Demo**
   - Click "Test Rate Limiting" rapidly
   - Show server-side rate limiting in action
   - Discuss client-side vs server-side limiting

7. **Results Summary**
   - Review the results table showing all patterns
   - Highlight success rates and timing data

### Key Talking Points

- **Polly v8 Benefits**: Modern async/await support, better performance, simplified API
- **Aspire Integration**: Built-in service discovery and resilience
- **Real-world Application**: How these patterns solve production problems
- **Observability**: Rich logging and metrics for monitoring

## 🛠️ API Endpoints

The API service exposes several endpoints with different characteristics:

| Endpoint                | Behavior              | Purpose                  |
| ----------------------- | --------------------- | ------------------------ |
| `/weatherforecast`      | Reliable              | Baseline for retry demos |
| `/unreliable-weather`   | 60% failure rate      | Circuit breaker demos    |
| `/slow-weather`         | Random 0.1s-8s delays | Timeout demos            |
| `/rate-limited-weather` | Server rate limiting  | Rate limiting demos      |

## 📊 Polly Configuration Examples

### Retry with Exponential Backoff + Jitter
```csharp
new ResiliencePipelineBuilder<WeatherForecast[]>()
    .AddRetry(new RetryStrategyOptions<WeatherForecast[]>
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true
    })
    .Build();
```

### Circuit Breaker
```csharp
new ResiliencePipelineBuilder<WeatherForecast[]>()
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions<WeatherForecast[]>
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(10),
        MinimumThroughput = 3,
        BreakDuration = TimeSpan.FromSeconds(10)
    })
    .Build();
```

### Timeout with Retry
```csharp
new ResiliencePipelineBuilder<WeatherForecast[]>()
    .AddTimeout(TimeSpan.FromSeconds(3))
    .AddRetry(new RetryStrategyOptions<WeatherForecast[]>
    {
        MaxRetryAttempts = 2,
        ShouldHandle = new PredicateBuilder<WeatherForecast[]>()
            .Handle<TimeoutRejectedException>()
    })
    .Build();
```

## 🎨 Customization Ideas

### For Your Presentations
- Modify failure rates in `ResiliencyDemo.ApiService/Program.cs`
- Adjust timeout values and retry counts
- Add your own resiliency patterns
- Customize the UI theme and branding
- Add metrics collection with OpenTelemetry

### Advanced Scenarios
- Add database with connection resilience
- Implement bulkhead isolation
- Add health checks with circuit breaker integration
- Demonstrate chaos engineering with chaos-monkey

## 📚 Additional Resources

- [Polly Documentation](https://github.com/App-vNext/Polly)
- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Resilience Patterns in Microservices](https://docs.microsoft.com/en-us/azure/architecture/patterns/category/resiliency)
- [Circuit Breaker Pattern](https://docs.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker)

## 🤝 Contributing

Feel free to fork this repository and customize it for your own presentations! Some ideas:
- Add more realistic failure scenarios
- Implement additional Polly patterns
- Add performance benchmarks
- Create Docker containerization examples

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Happy Presenting! 🎤** Make sure to practice the demo flow and have fun showing off the power of resilient .NET applications!