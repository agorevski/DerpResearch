using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using System.Runtime.CompilerServices;

namespace DeepResearch.WebApp.Agents;

public class SearchAgent : ISearchAgent
{
    private readonly ISearchService _searchService;
    private readonly IMemoryService _memoryService;
    private readonly IWebContentFetcher _contentFetcher;
    private readonly ILogger<SearchAgent> _logger;

    public SearchAgent(
        ISearchService searchService,
        IMemoryService memoryService,
        IWebContentFetcher contentFetcher,
        ILogger<SearchAgent> logger)
    {
        _searchService = searchService;
        _memoryService = memoryService;
        _contentFetcher = contentFetcher;
        _logger = logger;
    }

    public async IAsyncEnumerable<object> ExecuteSearchPlanAsync(ResearchPlan plan, int derpificationLevel = 100, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var allResults = new List<SearchResult>();
        var storedMemoryIds = new List<string>();

        // Determine number of results based on derpification level
        int resultsPerQuery = GetResultsPerQuery(derpificationLevel);

        _logger.LogInformation("Executing search plan with {Count} subtasks (fetching {Results} results per query)", 
            plan.Subtasks.Length, resultsPerQuery);

        foreach (var task in plan.Subtasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogDebug("Processing subtask: {Description}", task.Description);

            SearchResult[] results;
            Dictionary<string, string> fetchedContent;
            
            // Perform search with variable result count
            try
            {
                results = await _searchService.SearchAsync(task.SearchQuery, resultsPerQuery, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search for: {Query}", task.SearchQuery);
                continue;
            }
            
            if (results.Length == 0)
            {
                _logger.LogDebug("No results found for: {Query}", task.SearchQuery);
                continue;
            }

            // Fetch full webpage content in parallel with 5 second timeout
            _logger.LogInformation("Fetching full webpage content for {Count} URLs from query: {Query}", 
                results.Length, task.SearchQuery);
            var urls = results.Select(r => r.Url).ToArray();
            
            try
            {
                fetchedContent = await _contentFetcher.FetchContentAsync(urls, timeoutSeconds: 5, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch content for: {Query}", task.SearchQuery);
                continue;
            }
            
            _logger.LogInformation("Successfully fetched content for {Success} out of {Total} URLs", 
                fetchedContent.Count, urls.Length);

            // Filter to only include results where content was successfully fetched
            var successfulResults = results
                .Where(r => fetchedContent.ContainsKey(r.Url))
                .Select(r => 
                {
                    r.Content = fetchedContent[r.Url];
                    return r;
                })
                .ToArray();

            if (successfulResults.Length == 0)
            {
                _logger.LogWarning("Failed to fetch content for any results for: {Query}", task.SearchQuery);
                continue;
            }

            allResults.AddRange(successfulResults);

            // Store each result in memory with full content and yield it immediately
            foreach (var result in successfulResults)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Use full content instead of just snippet
                var memoryText = $"{result.Title}\n{result.Content}\nSource: {result.Url}";
                var tags = new[] { "search-result", task.SearchQuery };

                string memoryId;
                try
                {
                    memoryId = await _memoryService.StoreMemoryAsync(memoryText, result.Url, tags, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to store memory for: {Url}", result.Url);
                    continue;
                }

                storedMemoryIds.Add(memoryId);
                
                // Yield the source immediately after fetching and storing
                yield return result;
            }

            _logger.LogDebug("Fetched and stored {Count} out of {Total} results for: {Query}", 
                successfulResults.Length, results.Length, task.SearchQuery);

            // Small delay to avoid rate limiting
            await Task.Delay(500, cancellationToken);
        }

        var info = new GatheredInformation
        {
            Results = allResults.ToArray(),
            StoredMemoryIds = storedMemoryIds.ToArray(),
            TotalSourcesFound = allResults.Count
        };

        _logger.LogInformation("Search complete: {Count} total sources gathered", info.TotalSourcesFound);

        // Yield the final gathered information
        yield return info;
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
