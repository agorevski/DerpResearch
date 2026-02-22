namespace DeepResearch.WebApp.Interfaces;

/// <summary>
/// Handles storage and retrieval of clarification questions.
/// </summary>
public interface IClarificationStorage
{
    /// <summary>
    /// Store clarification questions for a conversation.
    /// </summary>
    Task StoreClarificationQuestionsAsync(string conversationId, string[] questions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve clarification questions for a conversation.
    /// </summary>
    Task<string[]?> GetClarificationQuestionsAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear clarification questions after they've been answered.
    /// </summary>
    Task ClearClarificationQuestionsAsync(string conversationId, CancellationToken cancellationToken = default);
}
