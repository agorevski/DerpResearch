namespace DeepResearch.WebApp.Services;

/// <summary>
/// Simple circuit breaker implementation
/// </summary>
public class CircuitBreaker
{
    private enum CircuitState { Closed, Open, HalfOpen }

    public const int DefaultFailureThreshold = 5;
    public const int DefaultBreakDurationSeconds = 30;
    public static readonly TimeSpan DefaultBreakDuration = TimeSpan.FromSeconds(DefaultBreakDurationSeconds);

    private CircuitState _state = CircuitState.Closed;
    private int _failureCount = 0;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private readonly int _failureThreshold;
    private readonly TimeSpan _breakDuration;
    private readonly ILogger _logger;
    private readonly object _lock = new();

    public CircuitBreaker(int failureThreshold, TimeSpan breakDuration, ILogger logger)
    {
        _failureThreshold = failureThreshold;
        _breakDuration = breakDuration;
        _logger = logger;
    }

    public bool AllowRequest()
    {
        lock (_lock)
        {
            if (_state == CircuitState.Closed)
            {
                return true;
            }

            if (_state == CircuitState.Open)
            {
                // Check if break duration has elapsed
                if (DateTime.UtcNow - _lastFailureTime >= _breakDuration)
                {
                    _logger.LogInformation("Circuit breaker entering HALF-OPEN state");
                    _state = CircuitState.HalfOpen;
                    return true;
                }
                return false;
            }

            // Half-open state - allow one request through
            return true;
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            if (_state == CircuitState.HalfOpen)
            {
                _logger.LogInformation("Circuit breaker closing after successful request");
                _state = CircuitState.Closed;
            }
            _failureCount = 0;
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_state == CircuitState.HalfOpen)
            {
                _logger.LogWarning("Circuit breaker reopening after failure in half-open state");
                _state = CircuitState.Open;
            }
            else if (_failureCount >= _failureThreshold)
            {
                _logger.LogError(
                    "Circuit breaker OPENING after {Count} failures (threshold: {Threshold})",
                    _failureCount, _failureThreshold);
                _state = CircuitState.Open;
            }
        }
    }
}
