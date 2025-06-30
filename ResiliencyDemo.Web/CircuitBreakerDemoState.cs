namespace ResiliencyDemo.Web;

public class CircuitBreakerDemoState
{
    private int _requestCount = 0;
    private readonly object _lock = new object();

    public int RequestCount => _requestCount;

    /// <summary>
    /// Deterministic demo sequence:
    /// Request 1: Success
    /// Request 2: Fail  
    /// Request 3: Fail (circuit opens)
    /// Request 4: Fail (circuit is open - fail fast)
    /// Request 5: Fail once in half-open (circuit re-opens)
    /// Request 6+: Success (circuit closes and stays closed)
    /// </summary>
    public void OnRequestAttempt()
    {
        lock (_lock)
        {
            _requestCount++;
        }
    }

    public string GetDemoBehavior()
    {
        lock (_lock)
        {
            return _requestCount switch
            {
                1 => "force-success",  // First request succeeds
                2 => "force-fail",     // Second request fails
                3 => "force-fail",     // Third request fails (circuit opens)
                4 => "force-fail",     // Fourth request fails fast (circuit is open)
                5 => "force-fail",     // Fifth request fails in half-open (circuit re-opens)
                _ => "force-success"   // All subsequent requests succeed (circuit closes)
            };
        }
    }

    public string GetBehaviorDescription()
    {
        lock (_lock)
        {
            return _requestCount switch
            {
                0 => "Ready for demo - first request will succeed",
                1 => "Next 2 requests will fail to trigger circuit breaker",
                2 => "Next request will fail and open the circuit",
                3 => "Circuit should be OPEN - next request will fail fast",
                4 => "Circuit should go HALF-OPEN - next request will fail and re-open circuit",
                5 => "Circuit re-opened - next request will succeed and close circuit",
                _ => "Circuit CLOSED - all requests now succeed"
            };
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _requestCount = 0;
        }
    }

    // These methods are called by Polly - we just log for debugging
    public void OnCircuitOpened() => Console.WriteLine($"� Circuit OPENED after request {_requestCount}");
    public void OnCircuitClosed() => Console.WriteLine($"✅ Circuit CLOSED after request {_requestCount}");  
    public void OnCircuitHalfOpened() => Console.WriteLine($"🔍 Circuit HALF-OPEN after request {_requestCount}");
}
