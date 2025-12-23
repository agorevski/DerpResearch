using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Middleware;

/// <summary>
/// Middleware for correlation ID propagation in distributed tracing.
/// Fixes anti-pattern #14: Missing Correlation IDs.
/// 
/// Features:
/// - Reads correlation ID from incoming request header (X-Correlation-ID)
/// - Generates new correlation ID if not present
/// - Adds correlation ID to response headers
/// - Enriches all log scopes with correlation ID for the request duration
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public const string CorrelationIdHeaderName = "X-Correlation-ID";
    public const string CorrelationIdItemKey = "CorrelationId";

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try to get correlation ID from incoming request header
        var correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault();
        
        // Generate new correlation ID if not provided
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = CorrelationId.New().Value;
        }

        // Store in HttpContext.Items for access throughout the request
        context.Items[CorrelationIdItemKey] = correlationId;

        // Add to response headers so clients can correlate their requests
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Enrich all logs with correlation ID for the duration of this request
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationIdItemKey] = correlationId
        }))
        {
            _logger.LogDebug("Request started with CorrelationId: {CorrelationId}", correlationId);
            
            await _next(context);
            
            _logger.LogDebug("Request completed with CorrelationId: {CorrelationId}", correlationId);
        }
    }
}

/// <summary>
/// Extension methods for correlation ID middleware registration.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// Adds the correlation ID middleware to the application pipeline.
    /// Should be added early in the pipeline to ensure all requests get correlation IDs.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}

/// <summary>
/// Extension methods for accessing correlation ID from HttpContext.
/// </summary>
public static class CorrelationIdContextExtensions
{
    /// <summary>
    /// Gets the correlation ID from the current HTTP context.
    /// Returns null if no correlation ID is available.
    /// </summary>
    public static string? GetCorrelationId(this HttpContext context)
    {
        return context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var value) 
            ? value?.ToString() 
            : null;
    }

    /// <summary>
    /// Gets the correlation ID or generates a new one if not present.
    /// Useful for background services that may not have an HTTP context.
    /// </summary>
    public static string GetOrCreateCorrelationId(this HttpContext? context)
    {
        var correlationId = context?.GetCorrelationId();
        return string.IsNullOrWhiteSpace(correlationId) 
            ? CorrelationId.New().Value 
            : correlationId;
    }
}
