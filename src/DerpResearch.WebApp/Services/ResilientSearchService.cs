using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using System.Collections.Concurrent;

namespace DeepResearch.WebApp.Services;

/// <summary>
/// Resilient wrapper for search service with circuit breaker, retry, and rate limiting patterns
/// </summary>
public class ResilientSearchService : ISearchService
{
    private readonly ISearchService _innerService;
    private readonly ILogger<ResilientSearchService> _logger;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly CircuitBreaker _circuitBreaker;
    private DateTime _lastRequest = DateTime.MinValue;
    private readonly TimeSpan _minimumInterval;
    private readonly object _lock = new();

    public ResilientSearchService(
        ISearchService innerService,
        ILogger<ResilientSearchService> logger,
        int maxConcurrentRequests = 2,
        int requestsPerSecond = 1)
    {
        _innerService = innerService;
        _logger = logger;
        _rateLimiter = new SemaphoreSlim(maxConcurrentRequests);
        _minimumInterval = TimeSpan.FromSeconds(1.0 / requestsPerSecond);
        _circuitBreaker = new CircuitBreaker(
            failureThreshold: 5,
            breakDuration: TimeSpan.FromSeconds(30),
            logger);
    }

    public async Task<SearchResult[]> SearchAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        // Check circuit breaker
        if (!_circuitBreaker.AllowRequest())
        {
            _logger.LogWarning("Circuit breaker is OPEN - search request rejected for query: {Query}", query);
            return Array.Empty<SearchResult>();
        }

        await _rateLimiter.WaitAsync();
        try
        {
            // Enforce rate limiting
            lock (_lock)
            {
                var timeSinceLastRequest = DateTime.UtcNow - _lastRequest;
                if (timeSinceLastRequest < _minimumInterval)
                {
                    var delay = _minimumInterval - timeSinceLastRequest;
                    Thread.Sleep(delay); // Synchronous wait under lock
                }
                _lastRequest = DateTime.UtcNow;
            }

            // Retry logic with exponential backoff
            int maxRetries = 3;
            Exception? lastException = null;
            
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        var result = await _innerService.SearchAsync(query, maxResults, cancellationToken);
                        _circuitBreaker.RecordSuccess();
                        return result;
                    }
                    catch (HttpRequestException ex) when (attempt < maxRetries)
                    {
                        lastException = ex;
                        var delaySeconds = Math.Pow(2, attempt);
                        _logger.LogWarning(ex, 
                            "Search attempt {Attempt}/{Max} failed for query: {Query}. Retrying after {Delay}s",
                            attempt, maxRetries, query, delaySeconds);
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        if (attempt < maxRetries)
                        {
                            var delaySeconds = Math.Pow(2, attempt);
                            _logger.LogWarning(ex, 
                                "Search attempt {Attempt}/{Max} failed for query: {Query}. Retrying after {Delay}s",
                                attempt, maxRetries, query, delaySeconds);
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                        }
                        else
                        {
                            _logger.LogError(ex, "Search failed for query: {Query} after {Attempts} attempts", query, maxRetries);
                        }
                    }
                }

            // All retries exhausted
            _logger.LogError("All retry attempts exhausted for query: {Query}. Last exception: {Exception}", 
                query, lastException?.Message);
            _circuitBreaker.RecordFailure();
            return Array.Empty<SearchResult>();
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    public async Task ClearExpiredCacheAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        // Delegate to inner service
        await _innerService.ClearExpiredCacheAsync(cancellationToken);
    }
}

/// <summary>
/// Simple circuit breaker implementation
/// </summary>
public class CircuitBreaker
{
    private enum CircuitState { Closed, Open, HalfOpen }

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

/// <summary>
/// Resilient wrapper for web content fetcher
/// </summary>
public class ResilientWebContentFetcher : IWebContentFetcher
{
    private readonly IWebContentFetcher _innerService;
    private readonly ILogger<ResilientWebContentFetcher> _logger;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly int _timeoutSeconds;

    public ResilientWebContentFetcher(
        IWebContentFetcher innerService,
        ILogger<ResilientWebContentFetcher> logger,
        int timeoutSeconds = 5)
    {
        _innerService = innerService;
        _logger = logger;
        _timeoutSeconds = timeoutSeconds;
        _circuitBreaker = new CircuitBreaker(
            failureThreshold: 5,
            breakDuration: TimeSpan.FromSeconds(30),
            logger);
    }

    public async Task<Dictionary<string, string>> FetchContentAsync(string[] urls, int timeoutSeconds = 5, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (!_circuitBreaker.AllowRequest())
        {
            _logger.LogWarning("Circuit breaker is OPEN - content fetch request rejected for {Count} URLs", urls.Length);
            return new Dictionary<string, string>();
        }

        try
        {
            var result = await _innerService.FetchContentAsync(urls, _timeoutSeconds, cancellationToken);
            _circuitBreaker.RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch content for {Count} URLs", urls.Length);
            _circuitBreaker.RecordFailure();
            return new Dictionary<string, string>();
        }
    }
}
