using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DeepResearch.WebApp.Services;

public class SearchService : ISearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SearchService> _logger;
    private readonly string _dbPath;
    private readonly int _cacheDuration;
    private readonly int _maxResults;
    private readonly string _googleApiKey;
    private readonly string _googleSearchEngineId;

    public SearchService(
        IHttpClientFactory httpClientFactory,
        IOptions<MemoryConfiguration> memoryConfig,
        IOptions<SearchConfiguration> searchConfig,
        IOptions<GoogleCustomSearchConfiguration> googleConfig,
        ILogger<SearchService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
        _dbPath = memoryConfig.Value.DatabasePath;
        _cacheDuration = searchConfig.Value.CacheDuration;
        _maxResults = searchConfig.Value.MaxResults;
        
        if (string.IsNullOrEmpty(googleConfig.Value.ApiKey))
            throw new InvalidOperationException("Google Custom Search API key not configured");
        if (string.IsNullOrEmpty(googleConfig.Value.SearchEngineId))
            throw new InvalidOperationException("Google Custom Search Engine ID not configured");
            
        _googleApiKey = googleConfig.Value.ApiKey;
        _googleSearchEngineId = googleConfig.Value.SearchEngineId;
    }

    public async Task<SearchResult[]> SearchAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        // Check cache first
        var cached = await GetCachedResultsAsync(query, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Returning cached results for query: {Query}", query);
            return cached;
        }

        cancellationToken.ThrowIfCancellationRequested();
        
        // Perform Google Custom Search
        var results = await GoogleCustomSearchAsync(query, maxResults, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        
        // Cache the results
        await CacheResultsAsync(query, results, cancellationToken);

        return results;
    }

    private async Task<SearchResult[]> GoogleCustomSearchAsync(string query, int maxResults, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Google Custom Search API endpoint
            var url = $"https://www.googleapis.com/customsearch/v1?key={_googleApiKey}&cx={_googleSearchEngineId}&q={Uri.EscapeDataString(query)}&num={Math.Min(maxResults, 10)}";

            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            var searchResponse = JsonSerializer.Deserialize<GoogleSearchResponse>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (searchResponse?.Items == null || searchResponse.Items.Length == 0)
            {
                _logger.LogWarning("No results found for query: {Query}", query);
                return Array.Empty<SearchResult>();
            }

            var results = searchResponse.Items
                .Take(maxResults)
                .Select(item => new SearchResult
                {
                    Title = item.Title ?? "",
                    Url = item.Link ?? "",
                    Snippet = item.Snippet ?? ""
                })
                .ToArray();

            _logger.LogInformation("Retrieved {Count} results from Google Custom Search for query: {Query}", results.Length, query);
            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while performing Google Custom Search for: {Query}", query);
            return Array.Empty<SearchResult>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Google Custom Search response for: {Query}", query);
            return Array.Empty<SearchResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform Google Custom Search for: {Query}", query);
            return Array.Empty<SearchResult>();
        }
    }

    private async Task<SearchResult[]?> GetCachedResultsAsync(string query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var queryHash = ComputeHash(query);

        var connectionString = $"Data Source={_dbPath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Results, Timestamp
            FROM SearchCache
            WHERE QueryHash = $hash
        ";
        command.Parameters.AddWithValue("$hash", queryHash);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync())
        {
            var timestamp = DateTime.Parse(reader.GetString(1));
            var age = DateTime.UtcNow - timestamp;

            if (age.TotalSeconds < _cacheDuration)
            {
                var resultsJson = reader.GetString(0);
                return JsonSerializer.Deserialize<SearchResult[]>(resultsJson);
            }
        }

        return null;
    }

    private async Task CacheResultsAsync(string query, SearchResult[] results, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var queryHash = ComputeHash(query);
        var resultsJson = JsonSerializer.Serialize(results);

        var connectionString = $"Data Source={_dbPath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO SearchCache (QueryHash, Results, Timestamp)
            VALUES ($hash, $results, $timestamp)
        ";
        command.Parameters.AddWithValue("$hash", queryHash);
        command.Parameters.AddWithValue("$results", resultsJson);
        command.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearExpiredCacheAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var threshold = DateTime.UtcNow.AddSeconds(-_cacheDuration);

        var connectionString = $"Data Source={_dbPath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM SearchCache
            WHERE Timestamp < $threshold
        ";
        command.Parameters.AddWithValue("$threshold", threshold.ToString("O"));

        var deleted = await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Cleared {Count} expired cache entries", deleted);
    }

    private string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input.ToLowerInvariant());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
