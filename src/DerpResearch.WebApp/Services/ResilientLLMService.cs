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
        int failureThreshold = 5,
        int breakDurationSeconds = 30,
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
        string deploymentName = "gpt-4o",
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
                var delaySeconds = Math.Pow(2, attempt);
                _logger.LogWarning(ex,
                    "LLM streaming attempt {Attempt}/{Max} failed. Retrying after {Delay}s",
                    attempt, _maxRetryAttempts, delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
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

    public async Task<string> ChatCompletion(
        ChatMessage[] messages,
        string deploymentName = "gpt-4o",
        CancellationToken cancellationToken = default)
    {
        if (!_circuitBreaker.AllowRequest())
        {
            _logger.LogWarning("Circuit breaker is OPEN - LLM completion request rejected");
            throw new InvalidOperationException("LLM service is temporarily unavailable due to circuit breaker.");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        Exception? lastException = null;

        for (int attempt = 1; attempt <= _maxRetryAttempts; attempt++)
        {
            try
            {
                var result = await _innerService.ChatCompletion(messages, deploymentName, cts.Token);
                _circuitBreaker.RecordSuccess();
                return result;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("LLM completion request timed out after {Timeout}s", _timeoutSeconds);
                _circuitBreaker.RecordFailure();
                throw new TimeoutException($"LLM completion timed out after {_timeoutSeconds} seconds");
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < _maxRetryAttempts)
                {
                    var delaySeconds = Math.Pow(2, attempt);
                    _logger.LogWarning(ex,
                        "LLM completion attempt {Attempt}/{Max} failed. Retrying after {Delay}s",
                        attempt, _maxRetryAttempts, delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
            }
        }

        _circuitBreaker.RecordFailure();
        _logger.LogError("LLM completion failed after {Attempts} attempts", _maxRetryAttempts);
        throw lastException ?? new InvalidOperationException("LLM completion failed");
    }

    public async Task<float[]> GetEmbedding(string text, CancellationToken cancellationToken = default)
    {
        if (!_circuitBreaker.AllowRequest())
        {
            _logger.LogWarning("Circuit breaker is OPEN - LLM embedding request rejected");
            throw new InvalidOperationException("LLM service is temporarily unavailable due to circuit breaker.");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        Exception? lastException = null;

        for (int attempt = 1; attempt <= _maxRetryAttempts; attempt++)
        {
            try
            {
                var result = await _innerService.GetEmbedding(text, cts.Token);
                _circuitBreaker.RecordSuccess();
                return result;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("LLM embedding request timed out after {Timeout}s", _timeoutSeconds);
                _circuitBreaker.RecordFailure();
                throw new TimeoutException($"LLM embedding timed out after {_timeoutSeconds} seconds");
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < _maxRetryAttempts)
                {
                    var delaySeconds = Math.Pow(2, attempt);
                    _logger.LogWarning(ex,
                        "LLM embedding attempt {Attempt}/{Max} failed. Retrying after {Delay}s",
                        attempt, _maxRetryAttempts, delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
            }
        }

        _circuitBreaker.RecordFailure();
        _logger.LogError("LLM embedding failed after {Attempts} attempts", _maxRetryAttempts);
        throw lastException ?? new InvalidOperationException("LLM embedding failed");
    }

    public async Task<T?> GetStructuredOutput<T>(
        string prompt,
        string deploymentName = "gpt-4o",
        CancellationToken cancellationToken = default) where T : class
    {
        if (!_circuitBreaker.AllowRequest())
        {
            _logger.LogWarning("Circuit breaker is OPEN - LLM structured output request rejected");
            return null;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        Exception? lastException = null;

        for (int attempt = 1; attempt <= _maxRetryAttempts; attempt++)
        {
            try
            {
                var result = await _innerService.GetStructuredOutput<T>(prompt, deploymentName, cts.Token);
                _circuitBreaker.RecordSuccess();
                return result;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("LLM structured output request timed out after {Timeout}s", _timeoutSeconds);
                _circuitBreaker.RecordFailure();
                return null;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < _maxRetryAttempts)
                {
                    var delaySeconds = Math.Pow(2, attempt);
                    _logger.LogWarning(ex,
                        "LLM structured output attempt {Attempt}/{Max} failed. Retrying after {Delay}s",
                        attempt, _maxRetryAttempts, delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
            }
        }

        _circuitBreaker.RecordFailure();
        _logger.LogError("LLM structured output failed after {Attempts} attempts", _maxRetryAttempts);
        return null;
    }
}
