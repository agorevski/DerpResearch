namespace DeepResearch.WebApp.Models;

public record ChatRequest(
    string Prompt,
    string Mode = "deep-research",
    string? ConversationId = null,
    int DerpificationLevel = 100,
    string[]? ClarificationAnswers = null
);

public record ChatResponse(
    string ConversationId,
    string Message
);

public record StreamToken(
    string Token,
    string? ConversationId = null,
    string? Type = "content",
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
