using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DeepResearch.WebApp.Services;

/// <summary>
/// Simple performance monitoring service for tracking key metrics
/// </summary>
public class PerformanceMonitor
{
    private readonly ILogger<PerformanceMonitor> _logger;
    private readonly ConcurrentDictionary<string, long> _metrics = new();
    private readonly ConcurrentDictionary<string, Stopwatch> _timers = new();
    private readonly ConcurrentDictionary<string, TimeSpan> _elapsedTimers = new();

    public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
    {
        _logger = logger;
    }

    public void RecordMetric(string name, long value)
    {
        _metrics.AddOrUpdate(name, value, (_, existingValue) => existingValue + value);
    }

    public void StartTimer(string name)
    {
        _timers[name] = Stopwatch.StartNew();
    }

    public TimeSpan StopTimer(string name)
    {
        if (_timers.TryRemove(name, out var timer))
        {
            timer.Stop();
            _elapsedTimers[name] = timer.Elapsed;
            return timer.Elapsed;
        }
        return TimeSpan.Zero;
    }

    public void LogMetrics(string context = "")
    {
        _logger.LogInformation("Performance Metrics {Context}: {Metrics}", context, string.Join(", ", _metrics.Select(m => $"{m.Key}={m.Value}")));
    }

    public void LogTimer(string name)
    {
        if (_elapsedTimers.TryGetValue(name, out var elapsed))
        {
            _logger.LogInformation("Performance Timer {Name}: {Elapsed}", name, elapsed);
        }
    }
}

/// <summary>
/// Health check for performance metrics to detect when system is under stress
/// </summary>
public class PerformanceHealthCheck : IHealthCheck
{
    private readonly ILogger<PerformanceHealthCheck> _logger;
    private readonly PerformanceMonitor _monitor;

    public PerformanceHealthCheck(PerformanceMonitor monitor, ILogger<PerformanceHealthCheck> logger)
    {
        _monitor = monitor;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Simple health checks - add more specific metrics as needed
        var status = HealthStatus.Healthy;

        _logger.LogDebug("PerformanceHealthCheck executed");

        // Here we would check for specific issues like:
        // - High error rates
        // - Long response times
        // - Memory usage
        // - Resource exhaustion

        // For now, just report healthy status
        var result = new HealthCheckResult(status, "Performance monitoring running normally");
        return Task.FromResult(result);
    }
}
