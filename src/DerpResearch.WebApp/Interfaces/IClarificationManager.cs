using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Interfaces;

/// <summary>
/// Manages clarification question generation and prompt enhancement.
/// Extracted from OrchestratorService to follow Single Responsibility Principle.
/// </summary>
public interface IClarificationManager
{
    /// <summary>
    /// Generates clarifying questions for a research prompt.
    /// Returns null if clarification answers are already provided.
    /// </summary>
    Task<ClarificationResult?> GenerateClarificationAsync(
        string prompt,
        string conversationId,
        string context,
        int derpificationLevel,
        string[]? existingAnswers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enhances the original prompt with clarification Q&A pairs.
    /// </summary>
    Task<string> EnhancePromptWithClarificationsAsync(
        string originalPrompt,
        string conversationId,
        string[] clarificationAnswers,
        CancellationToken cancellationToken = default);
}
