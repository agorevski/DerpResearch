using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Services;

public class MockSearchService : ISearchService
{
    private readonly ILogger<MockSearchService> _logger;

    public MockSearchService(ILogger<MockSearchService> logger)
    {
        _logger = logger;
    }

    public Task<SearchResult[]> SearchAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("MockSearchService: Returning mock results for query: {Query}", query);

        maxResults = 2;
        // Generate mock results based on the query
        var results = new List<SearchResult>();
        
        for (int i = 0; i < Math.Min(maxResults, 5); i++)
        {
            results.Add(new SearchResult
            {
                Title = $"Mock Result {i + 1}: {query}",
                Url = $"https://example.com/result-{i + 1}?q={Uri.EscapeDataString(query)}",
                Snippet = $"This is a mock search result snippet for '{query}'. It contains relevant information about the topic and demonstrates how search results would appear in the application. This is result number {i + 1}."
            });
        }

        return Task.FromResult(results.ToArray());
    }

    public Task ClearExpiredCacheAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("MockSearchService: ClearExpiredCacheAsync called (no-op in mock)");
        return Task.CompletedTask;
    }
}
