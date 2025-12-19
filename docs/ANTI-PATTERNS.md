# Development Anti-Patterns

This document identifies development anti-patterns found in the DerpResearch codebase, organized by category with severity ratings, impact analysis, and recommended solutions.

## Severity Legend

- 🔴 **Critical**: Major architectural issues requiring immediate attention
- 🟡 **High**: Significant problems affecting maintainability or performance
- 🟢 **Medium**: Issues that should be addressed but aren't blocking
- 🔵 **Low**: Minor improvements for code quality

---

## Table of Contents

1. [Architecture Anti-Patterns](#architecture-anti-patterns)
2. [Async/Await Anti-Patterns](#asyncawait-anti-patterns)
3. [State Management Anti-Patterns](#state-management-anti-patterns)
4. [Configuration Anti-Patterns](#configuration-anti-patterns)
5. [Type Safety Anti-Patterns](#type-safety-anti-patterns)
6. [Database Anti-Patterns](#database-anti-patterns)
7. [Resilience Anti-Patterns](#resilience-anti-patterns)
8. [Domain Modeling Anti-Patterns](#domain-modeling-anti-patterns)
9. [Logging Anti-Patterns](#logging-anti-patterns)
10. [Testing Anti-Patterns](#testing-anti-patterns)

---

## Architecture Anti-Patterns

### 1. God Object Pattern 🔴

**Location**: `Services/OrchestratorService.cs`

**Issue**: The `OrchestratorService` class has grown to 600+ lines and handles too many responsibilities:

- Workflow orchestration
- Agent coordination
- Progress streaming
- Error handling
- Clarification question management
- Iterative research logic
- SSE message formatting
- Memory management

**Code Example**:

```csharp
public class OrchestratorService : IOrchestratorService
{
    // 10+ dependencies injected
    private readonly IClarificationAgent _clarificationAgent;
    private readonly IPlannerAgent _plannerAgent;
    private readonly ISearchAgent _searchAgent;
    private readonly ISynthesisAgent _synthesisAgent;
    private readonly IReflectionAgent _reflectionAgent;
    private readonly IMemoryService _memoryService;
    private readonly ISearchService _searchService;
    private readonly ILLMService _llmService;
    // ... plus 600 lines of complex logic
}
```

**Impact**:

- Difficult to test in isolation
- High coupling between components
- Hard to understand and modify
- Violates Single Responsibility Principle
- Merge conflicts likely in team environments

**Recommended Solution**:
Break down into smaller, focused services:

```csharp
// Separate concerns into distinct services
public interface IWorkflowCoordinator
{
    IAsyncEnumerable<WorkflowEvent> ExecuteWorkflowAsync(WorkflowRequest request);
}

public interface IProgressStreamingService
{
    IAsyncEnumerable<string> StreamProgressAsync(WorkflowProgress progress);
}

public interface IClarificationManager
{
    Task<ClarificationResult> HandleClarificationAsync(string prompt, string conversationId);
}

public interface IIterativeResearchManager
{
    Task<ResearchResult> ExecuteIterativeResearchAsync(ResearchPlan plan, int maxIterations);
}
```

**Migration Strategy**:

1. Extract clarification logic to `ClarificationManager`
2. Extract streaming logic to `ProgressStreamingService`
3. Extract iteration logic to `IterativeResearchManager`
4. Keep `OrchestratorService` as thin coordinator

---

## Async/Await Anti-Patterns

### 2. No Cancellation Token Support 🟡

**Location**: Multiple async methods throughout codebase

**Issue**: Long-running operations don't accept `CancellationToken` parameters, making them impossible to cancel.

**Code Example**:

```csharp
// OrchestratorService.cs - 600+ line workflow with no cancellation
public async IAsyncEnumerable<string> ProcessDeepResearchAsync(
    string prompt, 
    string conversationId, 
    int derpificationLevel = 100, 
    string[]? clarificationAnswers = null)
{
    // No way to cancel this expensive operation
    // Multiple LLM calls, web searches, embeddings...
}

// MemoryService.cs - Multiple expensive embedding calls
public async Task<string> StoreMemoryAsync(
    string text, 
    string source, 
    string[] tags, 
    string? conversationId = null)
{
    // Loops through chunks calling LLM API
    var embedding = await _llmService.GetEmbedding(chunk);
}
```

**Impact**:

- Cannot cancel expensive research operations
- Poor user experience (no way to stop long searches)
- Resource waste on abandoned requests
- Potential memory leaks from uncancelled async operations
- Costs money on abandoned LLM API calls

**Recommended Solution**:

```csharp
public async IAsyncEnumerable<string> ProcessDeepResearchAsync(
    string prompt, 
    string conversationId, 
    int derpificationLevel = 100, 
    string[]? clarificationAnswers = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    // Check cancellation at each step
    cancellationToken.ThrowIfCancellationRequested();
    
    var plan = await _plannerAgent.CreatePlanAsync(
        prompt, context, derpificationLevel, cancellationToken);
    
    // Pass through to all sub-operations
}

public async Task<string> StoreMemoryAsync(
    string text, 
    string source, 
    string[] tags, 
    string? conversationId = null,
    CancellationToken cancellationToken = default)
{
    foreach (var chunk in chunks)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var embedding = await _llmService.GetEmbedding(chunk, cancellationToken);
    }
}
```

---

## State Management Anti-Patterns

### 3. Static Mutable State 🟢

**Location**: `Services/OrchestratorService.cs`

**Issue**: Static JSON serializer options shared across instances:

```csharp
private static readonly JsonSerializerOptions _jsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};
```

**Impact**:

- Difficult to test with different configurations
- Hidden coupling between tests
- Cannot customize per-request

**Recommended Solution**:

```csharp
// Option 1: Inject configuration
public class JsonSerializationService
{
    public JsonSerializerOptions Options { get; }
    
    public JsonSerializationService(IConfiguration config)
    {
        Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}

// Option 2: Use ASP.NET Core's configured options
services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
```

---

## Configuration Anti-Patterns

### 4. Magic Strings Throughout Codebase 🟡

**Location**: Multiple files

**Issue**: Configuration keys as magic strings scattered throughout:

```csharp
// Program.cs
var azureEndpoint = builder.Configuration["AzureOpenAI:Endpoint"];
var dbPath = builder.Configuration["Memory:DatabasePath"];

// LLMService.cs
var deployment = _config[$"AzureOpenAI:Deployments:{deploymentName}"];

// OrchestratorService.cs
var maxIterations = int.Parse(_config["Reflection:MaxIterations"] ?? "2");
var confidenceThreshold = double.Parse(_config["Reflection:ConfidenceThreshold"] ?? "0.7");
```

**Impact**:

- Typos not caught at compile time
- Difficult to refactor
- Hard to find all usages
- Duplicate string literals
- No IntelliSense support

**Recommended Solution**:

```csharp
// Create strongly-typed configuration
public class AzureOpenAIConfiguration
{
    public const string Section = "AzureOpenAI";
    
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public DeploymentConfiguration Deployments { get; set; } = new();
}

public class DeploymentConfiguration
{
    public string Chat { get; set; } = "gpt-4o";
    public string ChatMini { get; set; } = "gpt-4o-mini";
    public string Embedding { get; set; } = "text-embedding-3-large";
}

public class MemoryConfiguration
{
    public const string Section = "Memory";
    
    public string DatabasePath { get; set; } = "Data/deepresearch.db";
    public int TopKResults { get; set; } = 5;
    public int MaxMemoryAge { get; set; } = 90;
}

public class ReflectionConfiguration
{
    public const string Section = "Reflection";
    
    public double ConfidenceThreshold { get; set; } = 0.7;
    public int MaxIterations { get; set; } = 2;
}

// Register in Program.cs
builder.Services.Configure<AzureOpenAIConfiguration>(
    builder.Configuration.GetSection(AzureOpenAIConfiguration.Section));
builder.Services.Configure<MemoryConfiguration>(
    builder.Configuration.GetSection(MemoryConfiguration.Section));
builder.Services.Configure<ReflectionConfiguration>(
    builder.Configuration.GetSection(ReflectionConfiguration.Section));

// Inject in services
public class LLMService : ILLMService
{
    private readonly AzureOpenAIConfiguration _config;
    
    public LLMService(IOptions<AzureOpenAIConfiguration> config)
    {
        _config = config.Value;
        var endpoint = _config.Endpoint; // Type-safe, IntelliSense-friendly
    }
}
```

---

### 5. Magic Numbers 🟢

**Location**: Multiple files

**Issue**: Hardcoded numbers without explanation:

```csharp
// MemoryService.cs
var chunks = TextChunker.ChunkText(text, maxTokens: 3000, overlapTokens: 100);

// SearchAgent.cs
await Task.Delay(500); // Why 500ms?

// WebContentFetcher.cs
fetchedContent = await _contentFetcher.FetchContentAsync(urls, timeoutSeconds: 5);
```

**Impact**:

- Unclear intent
- Hard to tune performance
- Difficult to test with different values
- No centralized configuration

**Recommended Solution**:

```csharp
public static class ChunkingConstants
{
    public const int MaxTokensPerChunk = 3000;
    public const int TokenOverlap = 100;
    
    // Documentation
    public const string Rationale = 
        "3000 tokens is conservative for text-embedding-3-large (8191 limit). " +
        "100 token overlap ensures context preservation across chunks.";
}

public static class RateLimitConstants
{
    public const int SearchDelayMilliseconds = 500;
    public const int WebFetchTimeoutSeconds = 5;
    public const int MaxConcurrentFetches = 5;
}

// Usage
var chunks = TextChunker.ChunkText(
    text, 
    maxTokens: ChunkingConstants.MaxTokensPerChunk, 
    overlapTokens: ChunkingConstants.TokenOverlap);
    
await Task.Delay(RateLimitConstants.SearchDelayMilliseconds);
```

---

## Type Safety Anti-Patterns

### 6. Primitive Obsession 🟡

**Location**: Throughout codebase

**Issue**: Using primitive strings for domain concepts:

```csharp
// Everywhere in the code
public async Task SaveMessageAsync(string conversationId, string role, string content)
public async Task<ConversationContext> GetConversationContextAsync(string conversationId)
public int AddVector(float[] embedding) // Returns int vectorId
```

**Impact**:

- Easy to mix up parameters
- No type safety
- No domain validation
- Hard to add behavior
- Difficult to refactor

**Recommended Solution**:

```csharp
// Strongly-typed domain identifiers
public readonly record struct ConversationId(string Value)
{
    public static ConversationId New() => new(Guid.NewGuid().ToString());
    public static ConversationId Parse(string value) => new(value);
}

public readonly record struct VectorId(int Value)
{
    public static VectorId New(int value) => new(value);
}

public readonly record struct MemoryId(string Value)
{
    public static MemoryId New() => new(Guid.NewGuid().ToString());
}

// Strongly-typed roles
public enum MessageRole
{
    System,
    User,
    Assistant
}

// Usage - compile-time type safety
public async Task SaveMessageAsync(
    ConversationId conversationId, 
    MessageRole role, 
    string content)
{
    // Cannot accidentally pass conversationId to role parameter
}
```

---

### 7. Stringly-Typed Code 🟢

**Location**: `Models/DTOs.cs`, streaming logic

**Issue**: Using string literals for message types:

```csharp
new StreamToken("", conversationId, "progress", ...)
new StreamToken("", conversationId, "source", ...)
new StreamToken("", conversationId, "search_query", ...)
new StreamToken("", conversationId, "reflection", ...)
new StreamToken("", conversationId, "clarification", ...)
new StreamToken("", conversationId, "done", ...)
```

**Impact**:

- Typos not caught until runtime
- Hard to find all message types
- No exhaustive switch support

**Recommended Solution**:

```csharp
public enum StreamTokenType
{
    Content,
    Progress,
    Source,
    SearchQuery,
    Reflection,
    Clarification,
    Plan,
    Done,
    Error
}

public record StreamToken(
    string Token,
    string ConversationId,
    StreamTokenType Type,
    object? Metadata = null
);

// Usage with exhaustive matching
StreamTokenType type = message.Type;
var result = type switch
{
    StreamTokenType.Content => HandleContent(message),
    StreamTokenType.Progress => HandleProgress(message),
    StreamTokenType.Source => HandleSource(message),
    // Compiler error if case is missing
    _ => throw new ArgumentOutOfRangeException()
};
```

---

## Database Anti-Patterns

### 8. Manual Connection Management 🟡

**Location**: `Services/MemoryService.cs`, `Memory/DatabaseInitializer.cs`

**Issue**: Manual creation and disposal of database connections throughout:

```csharp
await using var connection = _dbInitializer.CreateConnection();
await connection.OpenAsync();

var command = connection.CreateCommand();
command.CommandText = @"INSERT INTO ...";
command.Parameters.AddWithValue("$id", id);
await command.ExecuteNonQueryAsync();
```

**Impact**:

- Verbose, repetitive code
- Potential connection leaks if exception occurs
- No connection pooling visibility
- Difficult to test
- No transaction support

**Recommended Solution**:

```csharp
// Option 1: Use Dapper for cleaner data access
public class MemoryRepository : IMemoryRepository
{
    private readonly string _connectionString;
    
    public async Task SaveMessageAsync(Message message)
    {
        await using var connection = new SqliteConnection(_connectionString);
        
        await connection.ExecuteAsync(
            @"INSERT INTO Messages (Id, ConversationId, Role, Content, Timestamp)
              VALUES (@Id, @ConversationId, @Role, @Content, @Timestamp)",
            message);
    }
    
    public async Task<Message[]> GetMessagesAsync(string conversationId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        
        var messages = await connection.QueryAsync<Message>(
            @"SELECT * FROM Messages 
              WHERE ConversationId = @ConversationId 
              ORDER BY Timestamp DESC LIMIT @Limit",
            new { ConversationId = conversationId, Limit = 10 });
            
        return messages.ToArray();
    }
}

// Option 2: Use Entity Framework Core
public class DeepResearchDbContext : DbContext
{
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Memory> Memories { get; set; }
}

public class MemoryRepository : IMemoryRepository
{
    private readonly DeepResearchDbContext _context;
    
    public async Task SaveMessageAsync(Message message)
    {
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
    }
}
```

---

## Resilience Anti-Patterns

### 9. Missing Circuit Breaker for LLM Calls 🟡

**Location**: `Services/LLMService.cs`

**Issue**: While `SearchService` and `WebContentFetcher` have resilient wrappers, `LLMService` calls Azure OpenAI directly with no circuit breaker, retry, or rate limiting.

**Code Example**:

```csharp
public class LLMService : ILLMService
{
    // Direct calls to Azure OpenAI - no resilience
    public async Task<string> ChatCompletion(ChatMessage[] messages, string deploymentName = "gpt-4o")
    {
        var response = await chatClient.CompleteChatAsync(chatMessages);
        // What if API is down? Rate limited? Network timeout?
        return response.Value.Content[0].Text;
    }
    
    public async Task<float[]> GetEmbedding(string text)
    {
        var response = await embeddingClient.GenerateEmbeddingAsync(text);
        // No retry, no circuit breaker, no timeout
        return response.Value.ToFloats().ToArray();
    }
}
```

**Impact**:

- No protection against API failures or rate limits
- No backoff on transient errors
- Cascading failures when API is down
- Expensive API calls may retry indefinitely (Azure SDK may have internal retry)
- Poor user experience during Azure OpenAI outages
- Costs money on abandoned or failed requests

**Recommended Solution**:

```csharp
public class ResilientLLMService : ILLMService
{
    private readonly LLMService _innerService;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly ILogger<ResilientLLMService> _logger;
    
    public ResilientLLMService(LLMService innerService, ILogger<ResilientLLMService> logger)
    {
        _innerService = innerService;
        _circuitBreaker = new CircuitBreaker(
            failureThreshold: 5,
            breakDuration: TimeSpan.FromSeconds(30),
            logger);
        _logger = logger;
    }
    
    public async IAsyncEnumerable<string> ChatCompletionStream(
        ChatMessage[] messages,
        string deploymentName = "gpt-4o")
    {
        await foreach (var token in _circuitBreaker.ExecuteAsync(
            async () => _innerService.ChatCompletionStream(messages, deploymentName)))
        {
            yield return token;
        }
    }
    
    public async Task<float[]> GetEmbedding(string text)
    {
        return await _circuitBreaker.ExecuteAsync(
            async () => await RetryWithBackoffAsync(
                async () => await _innerService.GetEmbedding(text),
                maxRetries: 3));
    }
    
    private async Task<T> RetryWithBackoffAsync<T>(
        Func<Task<T>> operation,
        int maxRetries)
    {
        for (int i = 0; i <= maxRetries; i++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (i < maxRetries && IsTransient(ex))
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, i));
                _logger.LogWarning("Retry {Attempt}/{Max} after {Delay}s", 
                    i + 1, maxRetries, delay.TotalSeconds);
                await Task.Delay(delay);
            }
        }
        throw new InvalidOperationException("Max retries exceeded");
    }
}
```

---

### 10. No Timeout for LLM Operations 🟢

**Location**: `Services/MemoryService.cs`, `LLMService.cs`

**Issue**: LLM operations have no timeout configuration:

```csharp
// MemoryService.StoreMemoryAsync
var embedding = await _llmService.GetEmbedding(chunk);
// Can hang indefinitely if Azure OpenAI is slow or unresponsive

// LLMService.ChatCompletion
var response = await chatClient.CompleteChatAsync(chatMessages);
// No timeout specified
```

**Impact**:

- Operations can hang indefinitely on network issues
- No way to fail fast during outages
- Poor user experience during slow responses
- Thread pool exhaustion from hanging requests
- Difficult to detect and diagnose slow API responses

**Recommended Solution**:

```csharp
public class LLMConfiguration
{
    public int ChatTimeoutSeconds { get; set; } = 30;
    public int EmbeddingTimeoutSeconds { get; set; } = 10;
    public int StreamingTimeoutSeconds { get; set; } = 60;
}

public class LLMService : ILLMService
{
    private readonly LLMConfiguration _config;
    
    public async Task<float[]> GetEmbedding(
        string text,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_config.EmbeddingTimeoutSeconds));
        
        try
        {
            var response = await embeddingClient.GenerateEmbeddingAsync(text, cts.Token);
            return response.Value.ToFloats().ToArray();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Embedding generation timed out after {_config.EmbeddingTimeoutSeconds}s");
        }
    }
}
```

---

## Domain Modeling Anti-Patterns

### 11. Anemic Domain Models 🟡

**Location**: `Models/Entities.cs`, `Models/DTOs.cs`

**Issue**: Domain models are just data containers with no behavior:

```csharp
public class Conversation
{
    public string Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    // No methods, no validation, no business logic
}

public class Message
{
    public string Id { get; set; }
    public string ConversationId { get; set; }
    public string Role { get; set; }
    public string Content { get; set; }
    // No behavior
}
```

**Impact**:

- Business logic scattered in services
- Difficult to enforce invariants
- Low cohesion
- Hard to find domain rules

**Recommended Solution**:

```csharp
public class Conversation
{
    private readonly List<Message> _messages = new();
    
    public ConversationId Id { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();
    
    public static Conversation Create()
    {
        return new Conversation
        {
            Id = ConversationId.New(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
    
    public void AddMessage(MessageRole role, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Message content cannot be empty", nameof(content));
            
        var message = Message.Create(Id, role, content);
        _messages.Add(message);
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void ArchiveOldMessages(int keepCount)
    {
        if (_messages.Count <= keepCount) return;
        
        var toRemove = _messages.Count - keepCount;
        _messages.RemoveRange(0, toRemove);
        UpdatedAt = DateTime.UtcNow;
    }
}

public class Message
{
    public MessageId Id { get; private set; }
    public ConversationId ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; }
    public DateTime Timestamp { get; private set; }
    
    internal static Message Create(ConversationId conversationId, MessageRole role, string content)
    {
        return new Message
        {
            Id = MessageId.New(),
            ConversationId = conversationId,
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow
        };
    }
    
    public bool IsFromUser() => Role == MessageRole.User;
    public bool IsFromAssistant() => Role == MessageRole.Assistant;
}
```

---

### 12. Missing Value Objects 🟢

**Location**: Throughout codebase

**Issue**: No value objects for domain concepts:

```csharp
// Everywhere - just primitives
public string Url { get; set; }
public string Title { get; set; }
public float ConfidenceScore { get; set; }
```

**Impact**:

- No validation
- No domain behavior
- Duplicate validation logic
- Easy to create invalid state

**Recommended Solution**:

```csharp
public readonly record struct Url
{
    public string Value { get; }
    
    public Url(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("URL cannot be empty", nameof(value));
            
        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
            throw new ArgumentException($"Invalid URL: {value}", nameof(value));
            
        Value = value;
    }
    
    public static implicit operator string(Url url) => url.Value;
}

public readonly record struct ConfidenceScore
{
    public double Value { get; }
    
    public ConfidenceScore(double value)
    {
        if (value < 0 || value > 1)
            throw new ArgumentException("Confidence must be between 0 and 1", nameof(value));
            
        Value = value;
    }
    
    public bool IsHighConfidence() => Value >= 0.7;
    public bool IsLowConfidence() => Value < 0.5;
    
    public static implicit operator double(ConfidenceScore score) => score.Value;
}
```

---

## Logging Anti-Patterns

### 13. Inconsistent Log Levels 🟢

**Location**: Multiple files

**Issue**: Inconsistent use of log levels:

```csharp
// Some places use Information for normal flow
_logger.LogInformation("Starting deep research for conversation {ConversationId}", conversationId);

// Other places use Debug
_logger.LogDebug("Processing subtask: {Description}", task.Description);

// Errors sometimes logged as Warning
_logger.LogWarning("Failed to fetch content for any results for: {Query}", task.SearchQuery);
```

**Impact**:

- Difficult to filter logs
- Inconsistent production logging
- Hard to set appropriate log levels

**Recommended Guidelines**:

```csharp
// Use consistent log levels across application

// TRACE: Very detailed, typically only enabled temporarily
_logger.LogTrace("Entering method with parameters: {Params}", parameters);

// DEBUG: Internal system events, useful for developers
_logger.LogDebug("Cache hit for query: {Query}", query);

// INFORMATION: General flow of application, important business events
_logger.LogInformation("Research workflow started for conversation {Id}", conversationId);
_logger.LogInformation("Found {Count} sources for query: {Query}", count, query);

// WARNING: Abnormal but expected conditions, degraded operations
_logger.LogWarning("Search returned no results for query: {Query}", query);
_logger.LogWarning("Falling back to general knowledge due to no sources");

// ERROR: Error conditions that affect specific operations but not entire app
_logger.LogError(ex, "Failed to fetch webpage content from {Url}", url);

// CRITICAL: Fatal errors requiring immediate attention
_logger.LogCritical(ex, "Database initialization failed - application cannot start");
```

---

### 14. Sensitive Data in Logs 🟡

**Location**: `Services/LLMService.cs`

**Issue**: Potential exposure of sensitive data:

```csharp
var maskedKey = apiKey != null && apiKey.Length > 8 
    ? $"{apiKey.Substring(0, 4)}...{apiKey.Substring(apiKey.Length - 4)}" 
    : "NOT_SET";

_logger.LogInformation("ChatCompletion - Deployment requested: {RequestedDeployment}, Resolved: {ResolvedDeployment}, Endpoint: {Endpoint}, ApiKey: {MaskedKey}",
    deploymentName, deployment, endpoint, maskedKey);
```

**Risk**: While key is masked, the pattern could be applied incorrectly elsewhere.

**Recommended Solution**:

```csharp
public static class LoggingExtensions
{
    public static string MaskSensitive(this string? value, int visibleChars = 4)
    {
        if (string.IsNullOrEmpty(value)) return "NOT_SET";
        if (value.Length <= visibleChars * 2) return "***";
        
        return $"{value.Substring(0, visibleChars)}...{value.Substring(value.Length - visibleChars)}";
    }
}

// Centralized sensitive data handling
public class SensitiveDataFilter : ILoggerProvider
{
    private readonly string[] _sensitivePatterns = 
    {
        "apikey", "api_key", "password", "secret", "token", "credential"
    };
    
    // Implement filtering logic
}

// Usage
_logger.LogInformation("Configuration - Endpoint: {Endpoint}, ApiKey: {ApiKey}",
    endpoint, apiKey.MaskSensitive());
```

---

### 15. Missing Correlation IDs 🟡

**Location**: Throughout request handling

**Issue**: No correlation ID for distributed tracing:

```csharp
_logger.LogInformation("Processing deep research request for conversation {ConversationId}", 
    conversationId);
```

**Impact**:

- Difficult to trace requests across services
- Hard to correlate logs from same request
- Poor observability

**Recommended Solution**:

```csharp
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString();
            
        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;
        
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await next(context);
        }
    }
}

// All logs automatically include correlation ID
_logger.LogInformation("Processing request"); // Includes CorrelationId in structured logs
```

---

## Testing Anti-Patterns

### 16. Hard to Test Services 🟡

**Location**: Multiple services

**Issue**: Services tightly coupled to external dependencies:

```csharp
public class LLMService : ILLMService
{
    public LLMService(IConfiguration config, ILogger<LLMService> logger)
    {
        var endpoint = new Uri(config["AzureOpenAI:Endpoint"]);
        _client = new AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey));
        // Hard to test - needs real Azure endpoint
    }
}
```

**Impact**:

- Cannot unit test without mocking framework
- Slow tests
- Requires Azure credentials in test environment
- Difficult to test edge cases

**Recommended Solution**:

```csharp
// Extract client creation to factory
public interface IAzureOpenAIClientFactory
{
    AzureOpenAIClient CreateClient();
}

public class LLMService : ILLMService
{
    private readonly AzureOpenAIClient _client;
    
    public LLMService(
        IAzureOpenAIClientFactory clientFactory,
        ILogger<LLMService> logger)
    {
        _client = clientFactory.CreateClient();
        _logger = logger;
    }
}

// Easy to mock in tests
public class MockAzureOpenAIClientFactory : IAzureOpenAIClientFactory
{
    public AzureOpenAIClient CreateClient()
    {
        // Return test double
        return new MockAzureOpenAIClient();
    }
}
```

---

### 17. Missing Integration Tests 🟢

**Location**: Test project

**Issue**: Only unit tests exist, no integration tests for:

- Database operations
- End-to-end workflows
- SSE streaming
- Multi-agent coordination

**Recommended Additions**:

```csharp
[Collection("Database")]
public class MemoryServiceIntegrationTests
{
    [Fact]
    public async Task StoreAndRetrieve_RoundTrip_Success()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var memoryService = factory.Services.GetRequiredService<IMemoryService>();
        
        // Act
        var memoryId = await memoryService.StoreMemoryAsync(
            "Test content", "test-source", new[] { "tag1" });
        var results = await memoryService.SearchMemoryAsync("Test", topK: 5);
        
        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Id == memoryId);
    }
}

[Collection("E2E")]
public class DeepResearchWorkflowTests
{
    [Fact]
    public async Task DeepResearch_EndToEnd_StreamsResults()
    {
        // Test full workflow with mocked external dependencies
    }
}
```

---

## Summary Table

| # | Anti-Pattern | Severity | Location |
|---|-------------|----------|----------|
| 1 | God Object | 🔴 Critical | OrchestratorService |
| 2 | No Cancellation Tokens | 🟡 High | Multiple async methods |
| 3 | Static Mutable State | 🟢 Medium | OrchestratorService |
| 4 | Magic Strings | 🟡 High | Multiple |
| 5 | Magic Numbers | 🟢 Medium | Multiple |
| 6 | Primitive Obsession | 🟡 High | Throughout |
| 7 | Stringly-Typed | 🟢 Medium | DTOs |
| 8 | Manual Connections | 🟡 High | MemoryService |
| 9 | Missing LLM Circuit Breaker | 🟡 High | LLMService |
| 10 | No LLM Timeouts | 🟢 Medium | LLMService |
| 11 | Anemic Models | 🟡 High | Entities |
| 12 | Missing Value Objects | 🟢 Medium | Throughout |
| 13 | Inconsistent Logging | 🟢 Medium | Multiple |
| 14 | Sensitive Data Logs | 🟡 High | LLMService |
| 15 | Missing Correlation IDs | 🟡 High | Logging |
| 16 | Hard to Test | 🟡 High | Services |
| 17 | Missing Integration Tests | 🟢 Medium | Tests |

## Priority Recommendations

### Immediate (Critical 🔴)

1. Break down OrchestratorService god object

### High Priority (🟡)

1. Add CancellationToken support to async operations
2. Implement circuit breaker for LLM calls
3. Fix exception swallowing in chunking loop
4. Add proper transaction support to database operations
5. Create strongly-typed configuration classes
6. Implement correlation IDs for observability

### Medium Priority (🟢)

1. Add timeouts for LLM operations
2. Extract hardcoded chunking constants to configuration
3. Convert StreamToken.Type to enum
4. Improve database connection management
5. Standardize logging practices
6. Add value objects for domain concepts
7. Improve test coverage with integration tests

## Migration Strategy

### Phase 1: Address Critical Issues (1-2 weeks)

1. Break down OrchestratorService into focused services
2. Add CancellationToken support to long-running operations

### Phase 2: Improve Architecture (2-3 weeks)

1. Implement circuit breaker and retry for LLM calls
2. Create strongly-typed configuration
3. Add proper transaction support

### Phase 3: Enhance Maintainability (2-3 weeks)

1. Implement correlation IDs
2. Standardize logging practices
3. Add value objects for domain concepts
4. Improve test coverage
5. Extract magic numbers to configuration

---

*Document created: 2025-11-15*
*Last updated: 2025-12-19*
