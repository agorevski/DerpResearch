namespace DeepResearch.WebApp.Models;

/// <summary>
/// Provider-agnostic request for LLM completion
/// </summary>
public record LLMRequest
{
    /// <summary>
    /// Messages in the conversation
    /// </summary>
    public required ChatMessage[] Messages { get; init; }

    /// <summary>
    /// Model/deployment name to use (provider-specific interpretation)
    /// </summary>
    public string ModelName { get; init; } = "gpt-4o";

    /// <summary>
    /// Temperature for response generation (0.0-2.0)
    /// </summary>
    public float Temperature { get; init; } = 0.7f;

    /// <summary>
    /// Maximum tokens in response
    /// </summary>
    public int? MaxTokens { get; init; }
}

/// <summary>
/// Provider-agnostic response from LLM completion
/// </summary>
public record LLMResponse
{
    /// <summary>
    /// The generated content
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Token usage statistics (if available)
    /// </summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>
    /// Model that generated the response
    /// </summary>
    public string? Model { get; init; }
}

/// <summary>
/// Token usage statistics
/// </summary>
public record TokenUsage
{
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens { get; init; }
}
