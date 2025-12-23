using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Interfaces;

/// <summary>
/// Service responsible for streaming progress updates to the client.
/// Extracted from OrchestratorService to follow Single Responsibility Principle.
/// </summary>
public interface IProgressStreamingService
{
    string CreateProgressToken(string conversationId, string stage, string message, object? details = null);
    string CreatePlanToken(string conversationId, string goal, string[] subtasks);
    string CreateSearchQueryToken(string conversationId, string query, int taskNumber, int totalTasks);
    string CreateSourceToken(string conversationId, string title, string url, string? snippet);
    string CreateClarificationToken(string conversationId, string[] questions, string rationale);
    string CreateReflectionToken(string conversationId, double confidenceScore, string reasoning, int iterations);
}
