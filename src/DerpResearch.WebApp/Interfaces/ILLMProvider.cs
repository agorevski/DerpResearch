using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Interfaces;

/// <summary>
/// Provider abstraction for Large Language Model services.
/// Allows swapping between Azure OpenAI, OpenAI, Anthropic, or other providers.
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// Stream completion responses token by token
    /// </summary>
    IAsyncEnumerable<string> StreamCompletionAsync(
        LLMRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a complete response (non-streaming)
    /// </summary>
    Task<string> CompleteAsync(
        LLMRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for text
    /// </summary>
    Task<float[]> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Provider name for logging and diagnostics
    /// </summary>
    string ProviderName { get; }
}
