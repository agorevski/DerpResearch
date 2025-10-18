using DeepResearch.WebApp.Agents;
using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Configure console logging for startup
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add logging for startup
var startupLogger = LoggerFactory.Create(config => 
{
    config.AddConsole();
    config.SetMinimumLevel(LogLevel.Information);
}).CreateLogger("Startup");

startupLogger.LogInformation("=== Application Starting ===");
startupLogger.LogInformation("Environment: {Environment}", builder.Environment.EnvironmentName);
startupLogger.LogInformation("Application Name: {AppName}", builder.Environment.ApplicationName);
startupLogger.LogInformation("Content Root: {ContentRoot}", builder.Environment.ContentRootPath);

// Log configuration sources
startupLogger.LogInformation("Configuration sources:");
foreach (var source in builder.Configuration.Sources)
{
    startupLogger.LogInformation("  - {Source}", source.GetType().Name);
}

// Validate critical configuration
startupLogger.LogInformation("Validating configuration...");
var azureEndpoint = builder.Configuration["AzureOpenAI:Endpoint"];
var azureApiKey = builder.Configuration["AzureOpenAI:ApiKey"];
var dbPath = builder.Configuration["Memory:DatabasePath"];

startupLogger.LogInformation("AzureOpenAI:Endpoint configured: {HasEndpoint}", !string.IsNullOrEmpty(azureEndpoint));
startupLogger.LogInformation("AzureOpenAI:ApiKey configured: {HasApiKey}", !string.IsNullOrEmpty(azureApiKey));
startupLogger.LogInformation("Memory:DatabasePath: {DbPath}", dbPath ?? "NOT SET");

if (string.IsNullOrEmpty(azureEndpoint))
{
    startupLogger.LogError("FATAL: AzureOpenAI:Endpoint is not configured!");
}
if (string.IsNullOrEmpty(azureApiKey))
{
    startupLogger.LogError("FATAL: AzureOpenAI:ApiKey is not configured!");
}

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add health checks with custom initialization check
builder.Services.AddSingleton<InitializationHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck<InitializationHealthCheck>("initialization", tags: new[] { "ready" });

// Add HttpClient for SearchService
builder.Services.AddHttpClient();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register LLM Service with error handling
try
{
    startupLogger.LogInformation("Registering LLM Service...");
    builder.Services.AddSingleton<ILLMService, LLMService>();
    startupLogger.LogInformation("LLM Service registered successfully");
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "Failed to register LLM Service: {Message}", ex.Message);
    throw;
}

// Register Memory Service with error handling
try
{
    startupLogger.LogInformation("Registering Memory Service...");
    builder.Services.AddSingleton<IMemoryService, MemoryService>();
    startupLogger.LogInformation("Memory Service registered successfully");
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "Failed to register Memory Service: {Message}", ex.Message);
    throw;
}

// Register Web Content Fetcher
builder.Services.AddSingleton<IWebContentFetcher, WebContentFetcher>();

// Register Search Service (use mock in Debug mode for testing)
// builder.Services.AddSingleton<ISearchService, MockSearchService>();
builder.Services.AddSingleton<ISearchService, SearchService>();

// Register Agents
builder.Services.AddSingleton<IClarificationAgent, ClarificationAgent>();
builder.Services.AddSingleton<IPlannerAgent, PlannerAgent>();
builder.Services.AddSingleton<ISearchAgent, SearchAgent>();
builder.Services.AddSingleton<ISynthesisAgent, SynthesisAgent>();
builder.Services.AddSingleton<IReflectionAgent, ReflectionAgent>();

// Register Orchestrator
builder.Services.AddSingleton<IOrchestratorService, OrchestratorService>();

startupLogger.LogInformation("Building application...");
WebApplication app;
try
{
    app = builder.Build();
    app.Logger.LogInformation("=== Application Built Successfully ===");
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "FATAL: Failed to build application: {Message}. Stack: {Stack}", 
        ex.Message, ex.StackTrace);
    throw;
}

// Register shutdown handlers
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() => 
{
    app.Logger.LogInformation("=== Application Started - Now accepting requests ===");
});
lifetime.ApplicationStopping.Register(() => 
{
    app.Logger.LogWarning("=== Application Stopping - Shutdown initiated ===");
});
lifetime.ApplicationStopped.Register(() => 
{
    app.Logger.LogWarning("=== Application Stopped ===");
});

// Initialize database in background (non-blocking)
var initHealthCheck = app.Services.GetRequiredService<InitializationHealthCheck>();
_ = Task.Run(async () =>
{
    try
    {
        app.Logger.LogInformation("Starting background initialization...");
        app.Logger.LogInformation("Retrieving MemoryService from DI container...");
        
        var memoryService = app.Services.GetRequiredService<IMemoryService>();
        app.Logger.LogInformation("MemoryService retrieved successfully");
        
        app.Logger.LogInformation("Calling InitializeAsync()...");
        await memoryService.InitializeAsync();
        
        app.Logger.LogInformation("Memory service initialized successfully");
        initHealthCheck.MarkAsHealthy();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to initialize memory service: {Message}. Stack: {Stack}", 
            ex.Message, ex.StackTrace);
        app.Logger.LogError("Inner exception: {InnerException}", ex.InnerException?.Message);
        initHealthCheck.MarkAsFailed(ex);
        
        // Don't crash the app - let it run in degraded mode
        app.Logger.LogWarning("Application will continue in degraded mode without memory service");
    }
});

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Only use HTTPS redirection in Development (Azure handles HTTPS at load balancer)
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseAuthorization();

// Map health checks endpoint - Azure App Service will use this
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (context, report) =>
    {
        app.Logger.LogInformation("Health check requested. Status: {Status}", report.Status);
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapControllers();

// Serve static files from wwwroot
app.UseStaticFiles();

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://+:8080";
app.Logger.LogInformation("Starting web server on: {Urls}", urls);
app.Logger.LogInformation("Process ID: {ProcessId}", Environment.ProcessId);
app.Logger.LogInformation("Working Directory: {WorkingDir}", Environment.CurrentDirectory);

// Add a startup delay to ensure all logging is captured
await Task.Delay(1000);
app.Logger.LogInformation("=== Web server starting now ===");

try
{
    app.Run();
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Application terminated unexpectedly: {Message}. Stack: {Stack}", 
        ex.Message, ex.StackTrace);
    throw;
}

// Custom health check for initialization status
public class InitializationHealthCheck : IHealthCheck
{
    private bool _isHealthy = false;
    private string _description = "Initializing...";
    private Exception? _exception = null;

    public void MarkAsHealthy()
    {
        _isHealthy = true;
        _description = "Initialization completed successfully";
    }

    public void MarkAsFailed(Exception ex)
    {
        _isHealthy = false;
        _description = $"Initialization failed: {ex.Message}";
        _exception = ex;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_isHealthy)
        {
            return Task.FromResult(HealthCheckResult.Healthy(_description));
        }
        
        if (_exception != null)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(_description, _exception));
        }

        // Still initializing - return degraded so Azure knows we're working on it
        return Task.FromResult(HealthCheckResult.Degraded(_description));
    }
}
