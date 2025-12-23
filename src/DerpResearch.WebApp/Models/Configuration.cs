namespace DeepResearch.WebApp.Models;

/// <summary>
/// Strongly-typed configuration for Reflection settings.
/// Eliminates magic strings for configuration keys.
/// </summary>
public class ReflectionConfiguration
{
    public const string Section = "Reflection";
    
    /// <summary>
    /// Confidence threshold for determining if response is good enough.
    /// Range: 0.0 to 1.0. Default: 0.7
    /// </summary>
    public double ConfidenceThreshold { get; set; } = 0.7;
    
    /// <summary>
    /// Maximum number of research iterations.
    /// </summary>
    public int MaxIterations { get; set; } = 2;
}

/// <summary>
/// Strongly-typed configuration for Memory settings.
/// </summary>
public class MemoryConfiguration
{
    public const string Section = "Memory";
    
    /// <summary>
    /// Path to the SQLite database file.
    /// </summary>
    public string DatabasePath { get; set; } = "Data/deepresearch.db";
    
    /// <summary>
    /// Path to the FAISS index file.
    /// </summary>
    public string FaissIndexPath { get; set; } = "Data/faiss.index";
    
    /// <summary>
    /// Maximum age of memories in days before compaction.
    /// </summary>
    public int MaxMemoryAge { get; set; } = 90;
    
    /// <summary>
    /// Number of top results to return in memory search.
    /// </summary>
    public int TopKResults { get; set; } = 5;
}

/// <summary>
/// Strongly-typed configuration for Azure OpenAI settings.
/// </summary>
public class AzureOpenAIConfiguration
{
    public const string Section = "AzureOpenAI";
    
    /// <summary>
    /// Azure OpenAI endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;
    
    /// <summary>
    /// Azure OpenAI API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Deployment names for different model types.
    /// </summary>
    public DeploymentConfiguration Deployments { get; set; } = new();
}

/// <summary>
/// Deployment names for Azure OpenAI models.
/// </summary>
public class DeploymentConfiguration
{
    /// <summary>
    /// Main chat model deployment name.
    /// </summary>
    public string Chat { get; set; } = "gpt-4o";
    
    /// <summary>
    /// Mini chat model for simpler tasks.
    /// </summary>
    public string ChatMini { get; set; } = "gpt-4o-mini";
    
    /// <summary>
    /// Embedding model deployment name.
    /// </summary>
    public string Embedding { get; set; } = "text-embedding-3-large";
}

/// <summary>
/// Strongly-typed configuration for Search settings.
/// </summary>
public class SearchConfiguration
{
    public const string Section = "Search";
    
    /// <summary>
    /// Cache duration for search results in seconds.
    /// </summary>
    public int CacheDuration { get; set; } = 86400;
    
    /// <summary>
    /// Maximum number of search results to return.
    /// </summary>
    public int MaxResults { get; set; } = 10;
}

/// <summary>
/// Strongly-typed configuration for Google Custom Search.
/// </summary>
public class GoogleCustomSearchConfiguration
{
    public const string Section = "GoogleCustomSearch";
    
    /// <summary>
    /// Google API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Custom Search Engine ID.
    /// </summary>
    public string SearchEngineId { get; set; } = string.Empty;
}

/// <summary>
/// Strongly-typed configuration for Mock services.
/// </summary>
public class MockServicesConfiguration
{
    public const string Section = "MockServices";
    
    /// <summary>
    /// Fixed confidence score to use when UseFixedConfidence is true.
    /// </summary>
    public float FixedConfidenceScore { get; set; } = 0.95f;
    
    /// <summary>
    /// Whether to use a fixed confidence score for testing.
    /// </summary>
    public bool UseFixedConfidence { get; set; } = false;
}

/// <summary>
/// Configuration for resilience patterns (circuit breaker, retry, rate limiting).
/// </summary>
public class ResilienceConfiguration
{
    public const string Section = "Resilience";
    
    /// <summary>
    /// Number of failures before opening the circuit breaker.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;
    
    /// <summary>
    /// Duration to keep circuit breaker open in seconds.
    /// </summary>
    public int BreakDurationSeconds { get; set; } = 30;
    
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
    
    /// <summary>
    /// Maximum concurrent requests for rate limiting.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 2;
    
    /// <summary>
    /// Requests per second for rate limiting.
    /// </summary>
    public int RequestsPerSecond { get; set; } = 1;
    
    /// <summary>
    /// Timeout for LLM operations in seconds.
    /// </summary>
    public int LLMTimeoutSeconds { get; set; } = 120;
}
