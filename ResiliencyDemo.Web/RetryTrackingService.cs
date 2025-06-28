namespace ResiliencyDemo.Web;

public class RetryTrackingService
{
    public event Action<ResilienceEvent>? OnResilienceEvent;

    public void LogEvent(string eventType, string pattern, string details, string? error = null)
    {
        OnResilienceEvent?.Invoke(new ResilienceEvent
        {
            EventType = eventType,
            Pattern = pattern,
            Details = details,
            Error = error,
            Timestamp = DateTime.Now
        });
    }
}

public class ResilienceEvent
{
    public string EventType { get; set; } = "";
    public string Pattern { get; set; } = "";
    public string Details { get; set; } = "";
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; }

    public string GetDisplayClass()
    {
        return EventType switch
        {
            "RETRY" => "table-warning",
            "TIMEOUT" => "table-info",
            "CIRCUIT_OPEN" => "table-danger",
            "CIRCUIT_CLOSED" => "table-success",
            "CIRCUIT_HALF_OPEN" => "table-warning",
            "SUCCESS" => "table-success",
            "FAILURE" => "table-danger",
            _ => ""
        };
    }

    public string GetIcon()
    {
        return EventType switch
        {
            "RETRY" => "🔄",
            "TIMEOUT" => "⏰",
            "CIRCUIT_OPEN" => "🚨",
            "CIRCUIT_CLOSED" => "✅",
            "CIRCUIT_HALF_OPEN" => "🔍",
            "SUCCESS" => "✅",
            "FAILURE" => "❌",
            _ => "📡"
        };
    }
}
