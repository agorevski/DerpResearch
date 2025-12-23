using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Interfaces;

/// <summary>
/// Manages the iterative research loop with reflection and additional searches.
/// Extracted from OrchestratorService to follow Single Responsibility Principle.
/// </summary>
public interface IIterativeResearchManager
{
    /// <summary>
    /// Executes the iterative synthesis and reflection loop.
    /// Yields tokens for streaming to the client.
    /// </summary>
    IAsyncEnumerable<string> ExecuteIterativeResearchAsync(
        string enhancedPrompt,
        ResearchPlan plan,
        GatheredInformation initialInfo,
        string conversationId,
        int derpificationLevel,
        CancellationToken cancellationToken = default);
}
