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

// Check if mock services should be used
var useMockServices = builder.Configuration.GetValue<bool>("UseMockServices", false);
var useResilientServices = builder.Configuration.GetValue<bool>("UseResilientServices", true);
startupLogger.LogInformation("=== SERVICE REGISTRATION MODE: {Mode} ===", 
    useMockServices ? "MOCK SERVICES" : "REAL SERVICES");
startupLogger.LogInformation("=== RESILIENCE PATTERNS: {Enabled} ===", 
    useResilientServices ? "ENABLED" : "DISABLED");

if (useMockServices)
{
    var useFixedConfidence = builder.Configuration.GetValue<bool>("MockServices:UseFixedConfidence", false);
    var fixedConfidenceScore = builder.Configuration.GetValue<float>("MockServices:FixedConfidenceScore", 0.95f);
    startupLogger.LogInformation("Mock Configuration:");
    startupLogger.LogInformation("  - UseFixedConfidence: {UseFixed}", useFixedConfidence);
    startupLogger.LogInformation("  - FixedConfidenceScore: {Score}", fixedConfidenceScore);
}

try
{
    // Register Web Content Fetcher
    // Register Search Service
    // Register Agents
    if (useMockServices)
    {
        startupLogger.LogInformation(">>> Registering MOCK Services:");
        startupLogger.LogInformation("  ✓ MockLLMService");
        builder.Services.AddSingleton<ILLMService, MockLLMService>();
        startupLogger.LogInformation("  ✓ MockWebContentFetcher");
        builder.Services.AddSingleton<IWebContentFetcher, MockWebContentFetcher>();
        startupLogger.LogInformation("  ✓ MockSearchService");
        builder.Services.AddSingleton<ISearchService, MockSearchService>();
        startupLogger.LogInformation("  ✓ MockClarificationAgent");
        builder.Services.AddSingleton<IClarificationAgent, MockClarificationAgent>();
        startupLogger.LogInformation("  ✓ MockPlannerAgent");
        builder.Services.AddSingleton<IPlannerAgent, MockPlannerAgent>();
        startupLogger.LogInformation("  ✓ MockSearchAgent");
        builder.Services.AddSingleton<ISearchAgent, MockSearchAgent>();
        startupLogger.LogInformation("  ✓ MockSynthesisAgent");
        builder.Services.AddSingleton<ISynthesisAgent, MockSynthesisAgent>();
        startupLogger.LogInformation("  ✓ MockReflectionAgent");
        builder.Services.AddSingleton<IReflectionAgent, MockReflectionAgent>();
        startupLogger.LogInformation(">>> All MOCK services registered successfully");
    }
    else
    {
        startupLogger.LogInformation(">>> Registering REAL Services:");
        startupLogger.LogInformation("  ✓ LLMService");
        builder.Services.AddSingleton<ILLMService, LLMService>();
        
        // Register services with optional resilience wrappers
        if (useResilientServices)
        {
            startupLogger.LogInformation("  ✓ WebContentFetcher (with resilience)");
            builder.Services.AddSingleton<WebContentFetcher>();
            builder.Services.AddSingleton<IWebContentFetcher>(sp =>
                new ResilientWebContentFetcher(
                    sp.GetRequiredService<WebContentFetcher>(),
                    sp.GetRequiredService<ILogger<ResilientWebContentFetcher>>()));
            
            startupLogger.LogInformation("  ✓ SearchService (with circuit breaker & rate limiting)");
            builder.Services.AddSingleton<SearchService>();
            builder.Services.AddSingleton<ISearchService>(sp =>
                new ResilientSearchService(
                    sp.GetRequiredService<SearchService>(),
                    sp.GetRequiredService<ILogger<ResilientSearchService>>()));
        }
        else
        {
            startupLogger.LogInformation("  ✓ WebContentFetcher (without resilience)");
            builder.Services.AddSingleton<IWebContentFetcher, WebContentFetcher>();
            startupLogger.LogInformation("  ✓ SearchService (without resilience)");
            builder.Services.AddSingleton<ISearchService, SearchService>();
        }
        
        startupLogger.LogInformation("  ✓ ClarificationAgent");
        builder.Services.AddSingleton<IClarificationAgent, ClarificationAgent>();
        startupLogger.LogInformation("  ✓ PlannerAgent");
        builder.Services.AddSingleton<IPlannerAgent, PlannerAgent>();
        startupLogger.LogInformation("  ✓ SearchAgent");
        builder.Services.AddSingleton<ISearchAgent, SearchAgent>();
        startupLogger.LogInformation("  ✓ SynthesisAgent");
        builder.Services.AddSingleton<ISynthesisAgent, SynthesisAgent>();
        startupLogger.LogInformation("  ✓ ReflectionAgent");
        builder.Services.AddSingleton<IReflectionAgent, ReflectionAgent>();
        startupLogger.LogInformation(">>> All REAL services registered successfully");
    }

    // Register Real Memory Service (No Mock available)
    builder.Services.AddSingleton<IMemoryService, MemoryService>();
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "Failed to register some service: {Message}", ex.Message);
    throw;
}
// Register Orchestrator
builder.Services.AddSingleton<IOrchestratorService, OrchestratorService>();

// Register database initialization as a hosted background service
builder.Services.AddHostedService<DatabaseInitializationService>();

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

// Database initialization now handled by DatabaseInitializationService (BackgroundService)
// No more fire-and-forget Task.Run - proper lifecycle management
app.Logger.LogInformation("Database initialization will be handled by DatabaseInitializationService");

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

// InitializationHealthCheck moved to DatabaseInitializationService.cs
