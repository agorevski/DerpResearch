using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;

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
            failureThreshold: CircuitBreaker.DefaultFailureThreshold,
            breakDuration: CircuitBreaker.DefaultBreakDuration,
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
            // Enforce rate limiting - check inside lock, delay outside
            TimeSpan delayNeeded = TimeSpan.Zero;
            lock (_lock)
            {
                var timeSinceLastRequest = DateTime.UtcNow - _lastRequest;
                if (timeSinceLastRequest < _minimumInterval)
                {
                    delayNeeded = _minimumInterval - timeSinceLastRequest;
                }
                _lastRequest = DateTime.UtcNow + delayNeeded;
            }

            if (delayNeeded > TimeSpan.Zero)
            {
                await Task.Delay(delayNeeded, cancellationToken);
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
                    catch (Exception ex)
                    {
                        lastException = ex;
                        if (attempt < maxRetries)
                        {
                            var delay = RetryHelper.GetBackoffDelay(attempt);
                            _logger.LogWarning(ex,
                                "Search attempt {Attempt}/{Max} failed for query: {Query}. Retrying after {Delay}s",
                                attempt, maxRetries, query, delay.TotalSeconds);
                            await Task.Delay(delay, cancellationToken);
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
