namespace ResiliencyDemo.Web;

public class CircuitBreakerDemoState
{
    // Simple step-based state machine for predictable demo flow
    private int _demoStep = 0;
    private readonly object _lock = new object();

    public enum DemoStep
    {
        InitialFailures = 0,    // Steps 0-2: Fail to trigger circuit open
        CircuitOpen = 3,        // Step 3: Circuit opened, fail fast
        FirstHalfOpen = 4,      // Step 4: First half-open attempt (will fail and re-open)
        CircuitReopened = 5,    // Step 5: Circuit re-opened after failed half-open
        SecondHalfOpen = 6,     // Step 6: Second half-open attempt (will succeed)
        PermanentlyHealthy = 7  // Step 7+: Always succeed
    }

    public int CurrentStep => _demoStep;
    public DemoStep CurrentDemoStep => (DemoStep)Math.Min(_demoStep, 7);

    // Called by Polly when circuit opens
    public void OnCircuitOpened()
    {
        lock (_lock)
        {
            // Don't reset the step - just log that circuit opened
            Console.WriteLine($"🚨 Circuit opened at demo step {_demoStep}");
        }
    }

    // Called by Polly when circuit closes
    public void OnCircuitClosed()
    {
        lock (_lock)
        {
            // Circuit closed successfully - move to permanently healthy state
            if (_demoStep == 6)
            {
                _demoStep = 7; // Move to permanently healthy
            }
            Console.WriteLine($"✅ Circuit closed at demo step {_demoStep}");
        }
    }

    // Called by Polly when circuit goes half-open
    public void OnCircuitHalfOpened()
    {
        lock (_lock)
        {
            // First time half-open: step 4, Second time: step 6
            if (_demoStep == 3)
            {
                _demoStep = 4; // First half-open
            }
            else if (_demoStep == 5)
            {
                _demoStep = 6; // Second half-open
            }
            Console.WriteLine($"🔍 Circuit half-open at demo step {_demoStep}");
        }
    }

    // Called before each request to get the behavior for this attempt
    public void OnRequestAttempt()
    {
        lock (_lock)
        {
            // Advance through different phases
            if (_demoStep < 3)
            {
                _demoStep++; // Initial failures: 0->1->2->3
                Console.WriteLine($"📈 Advanced to demo step {_demoStep}");
            }
            else if (_demoStep == 4)
            {
                // After first half-open failure, circuit re-opens
                _demoStep = 5; // Move to "circuit re-opened" state
                Console.WriteLine($"📈 Circuit re-opened, advanced to demo step {_demoStep}");
            }
        }
    }

    /// <summary>
    /// Gets the demo behavior query parameter to send to the server
    /// </summary>
    public string GetDemoBehavior()
    {
        lock (_lock)
        {
            return CurrentDemoStep switch
            {
                DemoStep.InitialFailures => "force-fail",        // Steps 0-2: Always fail
                DemoStep.CircuitOpen => "force-fail",            // Step 3: Fail fast (circuit open)
                DemoStep.FirstHalfOpen => "force-fail",          // Step 4: First half-open fails
                DemoStep.CircuitReopened => "force-fail",        // Step 5: Circuit re-opened, fail fast
                DemoStep.SecondHalfOpen => "force-success",      // Step 6: Second half-open succeeds
                DemoStep.PermanentlyHealthy => "force-success",  // Step 7+: Always succeed
                _ => "force-fail"
            };
        }
    }

    /// <summary>
    /// Gets a human-readable description of current demo behavior
    /// </summary>
    public string GetBehaviorDescription()
    {
        lock (_lock)
        {
            return CurrentDemoStep switch
            {
                DemoStep.InitialFailures => $"Building failure history (step {_demoStep}/3)",
                DemoStep.CircuitOpen => "Circuit OPEN - failing fast",
                DemoStep.FirstHalfOpen => "First half-open test (will fail and re-open circuit)",
                DemoStep.CircuitReopened => "Circuit RE-OPENED after failed half-open test",
                DemoStep.SecondHalfOpen => "Second half-open test (will succeed and close circuit)",
                DemoStep.PermanentlyHealthy => "Service recovered - permanently healthy",
                _ => "Demo step unknown"
            };
        }
    }

    /// <summary>
    /// Reset the demo to start over
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _demoStep = 0;
            Console.WriteLine("🔄 Demo state reset to step 0");
        }
    }
}
