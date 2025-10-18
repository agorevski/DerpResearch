using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Agents;

public class SearchAgent : ISearchAgent
{
    private readonly ISearchService _searchService;
    private readonly IMemoryService _memoryService;
    private readonly ILogger<SearchAgent> _logger;

    public SearchAgent(
        ISearchService searchService,
        IMemoryService memoryService,
        ILogger<SearchAgent> logger)
    {
        _searchService = searchService;
        _memoryService = memoryService;
        _logger = logger;
    }

    public async Task<GatheredInformation> ExecuteSearchPlanAsync(ResearchPlan plan, int derpificationLevel = 100)
    {
        var allResults = new List<SearchResult>();
        var storedMemoryIds = new List<string>();

        // Determine number of results based on derpification level
        int resultsPerQuery = GetResultsPerQuery(derpificationLevel);

        _logger.LogInformation("Executing search plan with {Count} subtasks (fetching {Results} results per query)", 
            plan.Subtasks.Length, resultsPerQuery);

        foreach (var task in plan.Subtasks)
        {
            _logger.LogDebug("Processing subtask: {Description}", task.Description);

            try
            {
                // Perform search with variable result count
                var results = await _searchService.SearchAsync(task.SearchQuery, resultsPerQuery);
                allResults.AddRange(results);

                // Store each result in memory with embeddings
                foreach (var result in results)
                {
                    var memoryText = $"{result.Title}\n{result.Snippet}\nSource: {result.Url}";
                    var tags = new[] { "search-result", task.SearchQuery };

                    var memoryId = await _memoryService.StoreMemoryAsync(
                        memoryText,
                        result.Url,
                        tags
                    );

                    storedMemoryIds.Add(memoryId);
                }

                _logger.LogDebug("Found and stored {Count} results for: {Query}", 
                    results.Length, task.SearchQuery);

                // Small delay to avoid rate limiting
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process subtask: {Description}", task.Description);
            }
        }

        var info = new GatheredInformation
        {
            Results = allResults.ToArray(),
            StoredMemoryIds = storedMemoryIds.ToArray(),
            TotalSourcesFound = allResults.Count
        };

        _logger.LogInformation("Search complete: {Count} total sources gathered", info.TotalSourcesFound);

        return info;
    }

    private int GetResultsPerQuery(int derpificationLevel)
    {
        if (derpificationLevel <= 33)
        {
            return 3; // Derp mode: Fewer results
        }
        else if (derpificationLevel <= 66)
        {
            return 5; // Average mode: Standard results
        }
        else
        {
            return 8; // Smart mode: More comprehensive results
        }
    }
}
