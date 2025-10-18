namespace DeepResearch.WebApp.Models;

public class ResearchPlan
{
    public string MainGoal { get; set; } = string.Empty;
    public ResearchTask[] Subtasks { get; set; } = Array.Empty<ResearchTask>();
    public string[] KeyConcepts { get; set; } = Array.Empty<string>();
}

public class ResearchTask
{
    public string Description { get; set; } = string.Empty;
    public string SearchQuery { get; set; } = string.Empty;
    public int Priority { get; set; }
}

public class GatheredInformation
{
    public SearchResult[] Results { get; set; } = Array.Empty<SearchResult>();
    public string[] StoredMemoryIds { get; set; } = Array.Empty<string>();
    public int TotalSourcesFound { get; set; }
}

public class ReflectionResult
{
    public double ConfidenceScore { get; set; }
    public string[] IdentifiedGaps { get; set; } = Array.Empty<string>();
    public string[] SuggestedAdditionalSearches { get; set; } = Array.Empty<string>();
    public bool RequiresMoreResearch { get; set; }
}

public class ClarificationResult
{
    public string[] Questions { get; set; } = Array.Empty<string>();
    public string Rationale { get; set; } = string.Empty;
}
