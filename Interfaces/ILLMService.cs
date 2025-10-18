using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Interfaces;

public interface ILLMService
{
    /// <summary>
    /// Chat completion with streaming
    /// </summary>
    IAsyncEnumerable<string> ChatCompletionStream(
        ChatMessage[] messages,
        string deploymentName = "gpt-4o"
    );

    /// <summary>
    /// Non-streaming chat completion for internal reasoning
    /// </summary>
    Task<string> ChatCompletion(
        ChatMessage[] messages,
        string deploymentName = "gpt-4o"
    );

    /// <summary>
    /// Generate embeddings for text
    /// </summary>
    Task<float[]> GetEmbedding(string text);

    /// <summary>
    /// Get structured JSON output from the model
    /// </summary>
    Task<T?> GetStructuredOutput<T>(
        string prompt,
        string deploymentName = "gpt-4o"
    ) where T : class;
}
