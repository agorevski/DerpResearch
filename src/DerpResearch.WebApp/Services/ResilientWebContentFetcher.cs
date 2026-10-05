using DeepResearch.WebApp.Interfaces;

namespace DeepResearch.WebApp.Services;

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
