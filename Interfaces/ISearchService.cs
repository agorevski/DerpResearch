using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Interfaces;

public interface ISearchService
{
    /// <summary>
    /// Search with caching
    /// </summary>
    Task<SearchResult[]> SearchAsync(string query, int maxResults = 10);

    /// <summary>
    /// Clear expired cache entries
    /// </summary>
    Task ClearExpiredCacheAsync();
}
