namespace DeepResearch.WebApp.Interfaces;

public interface IOrchestratorService
{
    /// <summary>
    /// Main entry point for deep research
    /// </summary>
    IAsyncEnumerable<string> ProcessDeepResearchAsync(
        string prompt, 
        string conversationId, 
        int derpificationLevel = 100, 
        string[]? clarificationAnswers = null);
}
