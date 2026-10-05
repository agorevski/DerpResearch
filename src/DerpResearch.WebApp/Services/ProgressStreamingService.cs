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
        return Serialize(conversationId, StreamTokenTypes.Progress, new ProgressUpdate(stage, message, details));
    }

    public string CreatePlanToken(string conversationId, string goal, string[] subtasks)
    {
        return Serialize(conversationId, StreamTokenTypes.Plan, new { goal, subtasks });
    }

    public string CreateSearchQueryToken(string conversationId, string query, int taskNumber, int totalTasks)
    {
        return Serialize(conversationId, StreamTokenTypes.SearchQuery, new SearchQueryUpdate(query, taskNumber, totalTasks));
    }

    public string CreateSourceToken(string conversationId, string title, string url, string? snippet)
    {
        return Serialize(conversationId, StreamTokenTypes.Source, new SourceUpdate(title, url, snippet));
    }

    public string CreateClarificationToken(string conversationId, string[] questions, string rationale)
    {
        return Serialize(conversationId, StreamTokenTypes.Clarification, new ClarificationUpdate(questions, rationale));
    }

    public string CreateReflectionToken(string conversationId, double confidenceScore, string reasoning, int iterations)
    {
        return Serialize(conversationId, StreamTokenTypes.Reflection, new ReflectionUpdate(confidenceScore, reasoning, iterations));
    }

    private string Serialize(string conversationId, string type, object payload)
    {
        return JsonSerializer.Serialize(new StreamToken("", conversationId, type, payload), _jsonOptions) + "\n";
    }
}
