using DeepResearch.WebApp.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DeepResearch.WebApp.Services;

/// <summary>
/// Background service that properly initializes the database on application startup.
/// Replaces the fire-and-forget Task.Run pattern with proper error handling and lifecycle management.
/// </summary>
public class DatabaseInitializationService : BackgroundService
{
    private readonly IMemoryService _memoryService;
    private readonly InitializationHealthCheck _healthCheck;
    private readonly ILogger<DatabaseInitializationService> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public DatabaseInitializationService(
        IMemoryService memoryService,
        InitializationHealthCheck healthCheck,
        ILogger<DatabaseInitializationService> logger,
        IHostApplicationLifetime lifetime)
    {
        _memoryService = memoryService;
        _healthCheck = healthCheck;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting database initialization...");
            await _memoryService.InitializeAsync();
            _healthCheck.MarkAsHealthy();
            _logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Database initialization failed: {Message}", ex.Message);
            _healthCheck.MarkAsFailed(ex);
            
            // Stop the application if database initialization fails
            // This prevents the app from running in an inconsistent state
            _logger.LogCritical("Stopping application due to critical database initialization failure");
            _lifetime.StopApplication();
        }
    }
}

/// <summary>
/// Health check for database initialization status
/// </summary>
public class InitializationHealthCheck : IHealthCheck
{
    private bool _isHealthy = false;
    private string _description = "Initializing...";
    private Exception? _exception = null;
    private readonly object _lock = new();

    public void MarkAsHealthy()
    {
        lock (_lock)
        {
            _isHealthy = true;
            _description = "Initialization completed successfully";
        }
    }

    public void MarkAsFailed(Exception ex)
    {
        lock (_lock)
        {
            _isHealthy = false;
            _description = $"Initialization failed: {ex.Message}";
            _exception = ex;
        }
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isHealthy)
            {
                return Task.FromResult(HealthCheckResult.Healthy(_description));
            }

            if (_exception != null)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(_description, _exception));
            }

            // Still initializing - return degraded
            return Task.FromResult(HealthCheckResult.Degraded(_description));
        }
    }
}
