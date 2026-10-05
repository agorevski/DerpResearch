using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using System.Runtime.CompilerServices;

namespace DeepResearch.WebApp.Services;

/// <summary>
/// Resilient wrapper for LLM service with circuit breaker and timeout patterns.
/// Addresses anti-pattern: No LLM Circuit Breaker.
/// </summary>
public class ResilientLLMService : ILLMService
{
    private readonly ILLMService _innerService;
    private readonly ILogger<ResilientLLMService> _logger;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly int _timeoutSeconds;
    private readonly int _maxRetryAttempts;

    public ResilientLLMService(
        ILLMService innerService,
        ILogger<ResilientLLMService> logger,
        int failureThreshold = CircuitBreaker.DefaultFailureThreshold,
        int breakDurationSeconds = CircuitBreaker.DefaultBreakDurationSeconds,
        int timeoutSeconds = 120,
        int maxRetryAttempts = 3)
    {
        _innerService = innerService;
        _logger = logger;
        _timeoutSeconds = timeoutSeconds;
        _maxRetryAttempts = maxRetryAttempts;
        _circuitBreaker = new CircuitBreaker(
            failureThreshold,
            TimeSpan.FromSeconds(breakDurationSeconds),
            logger);
    }

    public async IAsyncEnumerable<string> ChatCompletionStream(
        ChatMessage[] messages,
        string deploymentName = LLMConstants.DefaultDeploymentName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_circuitBreaker.AllowRequest())
        {
            _logger.LogWarning("Circuit breaker is OPEN - LLM streaming request rejected");
            yield break;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        // For streaming, we can't easily retry after we've started yielding,
        // so we attempt to get the stream and then yield from it
        IAsyncEnumerable<string>? stream = null;
        Exception? streamException = null;

        for (int attempt = 1; attempt <= _maxRetryAttempts; attempt++)
        {
            try
            {
                // Just verify the service is reachable by getting the enumerable
                stream = _innerService.ChatCompletionStream(messages, deploymentName, cts.Token);
                break;
            }
            catch (Exception ex) when (attempt < _maxRetryAttempts)
            {
                streamException = ex;
                var delay = RetryHelper.GetBackoffDelay(attempt);
                _logger.LogWarning(ex,
                    "LLM streaming attempt {Attempt}/{Max} failed. Retrying after {Delay}s",
                    attempt, _maxRetryAttempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM streaming request failed after {Attempts} attempts", attempt);
                _circuitBreaker.RecordFailure();
                throw;
            }
        }

        if (stream == null)
        {
            if (streamException != null)
            {
                _circuitBreaker.RecordFailure();
                throw streamException;
            }
            yield break;
        }

        // Now yield from the stream - we can't retry after this point
        IAsyncEnumerator<string>? enumerator = null;
        
        try
        {
            enumerator = stream.GetAsyncEnumerator(cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get async enumerator for LLM stream");
            _circuitBreaker.RecordFailure();
            throw;
        }

        try
        {
            while (true)
            {
                bool hasNext;
                string current;
                
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                    if (!hasNext) break;
                    current = enumerator.Current;
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("LLM streaming request timed out after {Timeout}s", _timeoutSeconds);
                    _circuitBreaker.RecordFailure();
                    yield break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "LLM streaming request failed during enumeration");
                    _circuitBreaker.RecordFailure();
                    throw;
                }
                
                yield return current;
            }
            
            _circuitBreaker.RecordSuccess();
        }
        finally
        {
            if (enumerator != null)
            {
                await enumerator.DisposeAsync();
            }
        }
    }

    public Task<string> ChatCompletion(
        ChatMessage[] messages,
        string deploymentName = LLMConstants.DefaultDeploymentName,
        CancellationToken cancellationToken = default)
        => RunWithResilienceAsync("completion",
            ct => _innerService.ChatCompletion(messages, deploymentName, ct),
            cancellationToken);

    public Task<float[]> GetEmbedding(string text, CancellationToken cancellationToken = default)
        => RunWithResilienceAsync("embedding",
            ct => _innerService.GetEmbedding(text, ct),
            cancellationToken);

    public async Task<T?> GetStructuredOutput<T>(
        string prompt,
        string deploymentName = LLMConstants.DefaultDeploymentName,
        CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            return await RunWithResilienceAsync("structured output",
                ct => _innerService.GetStructuredOutput<T>(prompt, deploymentName, ct),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Runs an LLM operation behind the circuit breaker, a per-request timeout,
    /// and exponential-backoff retries. Throws on open circuit, timeout, or
    /// exhausted retries; callers that prefer a null result catch and translate.
    /// </summary>
    private async Task<T> RunWithResilienceAsync<T>(
        string operation,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (!_circuitBreaker.AllowRequest())
        {
            _logger.LogWarning("Circuit breaker is OPEN - LLM {Operation} request rejected", operation);
            throw new InvalidOperationException("LLM service is temporarily unavailable due to circuit breaker.");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        Exception? lastException = null;

        for (int attempt = 1; attempt <= _maxRetryAttempts; attempt++)
        {
            try
            {
                var result = await action(cts.Token);
                _circuitBreaker.RecordSuccess();
                return result;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("LLM {Operation} request timed out after {Timeout}s", operation, _timeoutSeconds);
                _circuitBreaker.RecordFailure();
                throw new TimeoutException($"LLM {operation} timed out after {_timeoutSeconds} seconds");
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < _maxRetryAttempts)
                {
                    var delay = RetryHelper.GetBackoffDelay(attempt);
                    _logger.LogWarning(ex,
                        "LLM {Operation} attempt {Attempt}/{Max} failed. Retrying after {Delay}s",
                        operation, attempt, _maxRetryAttempts, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        _circuitBreaker.RecordFailure();
        _logger.LogError("LLM {Operation} failed after {Attempts} attempts", operation, _maxRetryAttempts);
        throw lastException ?? new InvalidOperationException($"LLM {operation} failed");
    }
}
