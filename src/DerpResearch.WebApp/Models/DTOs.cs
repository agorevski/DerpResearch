using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DeepResearch.WebApp.Models;

public record ChatRequest(
    [property: Required, MaxLength(10000)] string Prompt,
    string Mode = "deep-research",
    string? ConversationId = null,
    [property: Range(0, 100)] int DerpificationLevel = 100,
    string[]? ClarificationAnswers = null
);

public record ChatResponse(
    string ConversationId,
    string Message
);

/// <summary>
/// Type of stream token being sent to the client.
/// Replaces magic string for Type field.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StreamTokenType
{
    Content,
    Progress,
    Plan,
    SearchQuery,
    Source,
    Clarification,
    Reflection,
    SourcesComplete,
    Fallback,
    Error
}

/// <summary>
/// Helper class for StreamTokenType string conversions (for backward compatibility).
/// </summary>
public static class StreamTokenTypes
{
    public const string Content = "content";
    public const string Progress = "progress";
    public const string Plan = "plan";
    public const string SearchQuery = "search_query";
    public const string Source = "source";
    public const string Clarification = "clarification";
    public const string Reflection = "reflection";
    public const string SourcesComplete = "sources_complete";
    public const string Fallback = "fallback";
    public const string Error = "error";
}

public record StreamToken(
    string Token,
    string? ConversationId = null,
    string? Type = StreamTokenTypes.Content,
    object? Data = null
);

public record ProgressUpdate(
    string Stage,
    string Message,
    object? Details = null
);

public record SearchQueryUpdate(
    string Query,
    int TaskNumber,
    int TotalTasks
);

public record SourceUpdate(
    string Title,
    string Url,
    string? Snippet
);

public record ClarificationUpdate(
    string[] Questions,
    string Rationale
);

public record ReflectionUpdate(
    double ConfidenceScore,
    string Reasoning,
    int Iterations
);

// Google Custom Search API response DTOs
public record GoogleSearchResponse(
    SearchInformation? SearchInformation,
    GoogleSearchItem[]? Items
);

public record SearchInformation(
    double SearchTime,
    string TotalResults
);

public record GoogleSearchItem(
    string Title,
    string Link,
    string? Snippet,
    string? HtmlSnippet
);
