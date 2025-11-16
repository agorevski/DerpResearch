using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Interfaces;

public interface IMemoryService
{
    /// <summary>
    /// Initialize database and FAISS index
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Store new information with embedding
    /// </summary>
    Task<string> StoreMemoryAsync(string text, string source, string[] tags, string? conversationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hybrid search: FAISS similarity + SQLite metadata filtering
    /// </summary>
    Task<MemoryChunk[]> SearchMemoryAsync(string query, int topK = 5, string? conversationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent context for conversation
    /// </summary>
    Task<ConversationContext> GetConversationContextAsync(string conversationId, int messageCount = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save a message to conversation history
    /// </summary>
    Task SaveMessageAsync(string conversationId, string role, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new conversation
    /// </summary>
    Task<string> CreateConversationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Compact old memories (maintenance)
    /// </summary>
    Task CompactMemoriesAsync(DateTime olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Store clarification questions for a conversation
    /// </summary>
    Task StoreClarificationQuestionsAsync(string conversationId, string[] questions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve clarification questions for a conversation
    /// </summary>
    Task<string[]?> GetClarificationQuestionsAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear clarification questions after they've been answered
    /// </summary>
    Task ClearClarificationQuestionsAsync(string conversationId, CancellationToken cancellationToken = default);
}
