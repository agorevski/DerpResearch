using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Interfaces;

/// <summary>
/// Manages conversation lifecycle and message persistence.
/// </summary>
public interface IConversationManager
{
    /// <summary>
    /// Create a new conversation.
    /// </summary>
    Task<string> CreateConversationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent context for conversation.
    /// </summary>
    Task<ConversationContext> GetConversationContextAsync(string conversationId, int messageCount = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save a message to conversation history.
    /// </summary>
    Task SaveMessageAsync(string conversationId, string role, string content, CancellationToken cancellationToken = default);
}
