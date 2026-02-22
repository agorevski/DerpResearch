using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Interfaces;

/// <summary>
/// Handles memory storage, search, and lifecycle operations.
/// </summary>
public interface IMemoryStorage
{
    /// <summary>
    /// Initialize database and vector index.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Store new information with embedding.
    /// Returns detailed result with success/failure info for each chunk.
    /// </summary>
    Task<StoreMemoryResult> StoreMemoryAsync(string text, string source, string[] tags, string? conversationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hybrid search: vector similarity + metadata filtering.
    /// </summary>
    Task<MemoryChunk[]> SearchMemoryAsync(string query, int topK = 5, string? conversationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compact old memories (maintenance).
    /// </summary>
    Task CompactMemoriesAsync(DateTime olderThan, CancellationToken cancellationToken = default);
}
