namespace DeepResearch.WebApp.Interfaces;

public interface IOrchestratorService
{
    /// <summary>
    /// Main entry point for deep research mode
    /// </summary>
    IAsyncEnumerable<string> ProcessDeepResearchAsync(
        string prompt, 
        string conversationId, 
        int derpificationLevel = 100, 
        string[]? clarificationAnswers = null);

    /// <summary>
    /// Simple chat mode (direct LLM pass-through)
    /// </summary>
    IAsyncEnumerable<string> ProcessSimpleChatAsync(string prompt, string conversationId);
}
