namespace DeepResearch.WebApp.Interfaces;

public interface IWebContentFetcher
{
    /// <summary>
    /// Fetches webpage content for multiple URLs in parallel with timeout
    /// </summary>
    /// <param name="urls">URLs to fetch</param>
    /// <param name="timeoutSeconds">Timeout per request in seconds</param>
    /// <returns>Dictionary mapping successful URLs to their content</returns>
    Task<Dictionary<string, string>> FetchContentAsync(string[] urls, int timeoutSeconds = 5);
}
