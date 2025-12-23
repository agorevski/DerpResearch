using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using System.Text.Json;

namespace DeepResearch.WebApp.Services;

/// <summary>
/// Service responsible for streaming progress updates to the client.
/// Extracted from OrchestratorService to follow Single Responsibility Principle.
/// </summary>
public class ProgressStreamingService : IProgressStreamingService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string CreateProgressToken(string conversationId, string stage, string message, object? details = null)
    {
        return JsonSerializer.Serialize(new StreamToken(
            "",
            conversationId,
            StreamTokenTypes.Progress,
            new ProgressUpdate(stage, message, details)
        ), _jsonOptions) + "\n";
    }

    public string CreatePlanToken(string conversationId, string goal, string[] subtasks)
    {
        return JsonSerializer.Serialize(new StreamToken(
            "",
            conversationId,
            StreamTokenTypes.Plan,
            new { goal, subtasks }
        ), _jsonOptions) + "\n";
    }

    public string CreateSearchQueryToken(string conversationId, string query, int taskNumber, int totalTasks)
    {
        return JsonSerializer.Serialize(new StreamToken(
            "",
            conversationId,
            StreamTokenTypes.SearchQuery,
            new SearchQueryUpdate(query, taskNumber, totalTasks)
        ), _jsonOptions) + "\n";
    }

    public string CreateSourceToken(string conversationId, string title, string url, string? snippet)
    {
        return JsonSerializer.Serialize(new StreamToken(
            "",
            conversationId,
            StreamTokenTypes.Source,
            new SourceUpdate(title, url, snippet)
        ), _jsonOptions) + "\n";
    }

    public string CreateClarificationToken(string conversationId, string[] questions, string rationale)
    {
        return JsonSerializer.Serialize(new StreamToken(
            "",
            conversationId,
            StreamTokenTypes.Clarification,
            new ClarificationUpdate(questions, rationale)
        ), _jsonOptions) + "\n";
    }

    public string CreateReflectionToken(string conversationId, double confidenceScore, string reasoning, int iterations)
    {
        return JsonSerializer.Serialize(new StreamToken(
            "",
            conversationId,
            StreamTokenTypes.Reflection,
            new ReflectionUpdate(confidenceScore, reasoning, iterations)
        ), _jsonOptions) + "\n";
    }
}
