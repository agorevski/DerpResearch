using DeepResearch.WebApp.Interfaces;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace DeepResearch.WebApp.Services;

public class WebContentFetcher : IWebContentFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebContentFetcher> _logger;
    private const int MaxContentLength = 50000; // Limit content to avoid token issues

    public WebContentFetcher(
        IHttpClientFactory httpClientFactory,
        ILogger<WebContentFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<Dictionary<string, string>> FetchContentAsync(string[] urls, int timeoutSeconds = 5, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        _logger.LogInformation("Fetching content for {Count} URLs in parallel with {Timeout}s timeout", 
            urls.Length, timeoutSeconds);

        var tasks = urls.Select(url => FetchSingleUrlAsync(url, timeoutSeconds, cancellationToken)).ToArray();
        var results = await Task.WhenAll(tasks);

        var successfulResults = results
            .Where(r => r.Success)
            .ToDictionary(r => r.Url, r => r.Content);

        _logger.LogInformation("Successfully fetched {Success} out of {Total} URLs", 
            successfulResults.Count, urls.Length);

        return successfulResults;
    }

    private async Task<(string Url, bool Success, string Content)> FetchSingleUrlAsync(string url, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            
            var httpClient = _httpClientFactory.CreateClient();
            
            // Set a user agent to avoid being blocked
            httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var response = await httpClient.GetAsync(url, cts.Token);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch {Url}: {StatusCode}", url, response.StatusCode);
                return (url, false, string.Empty);
            }

            var html = await response.Content.ReadAsStringAsync(cts.Token);
            var textContent = ExtractTextFromHtml(html);

            _logger.LogDebug("Successfully fetched {Url} ({Length} chars)", url, textContent.Length);
            return (url, true, textContent);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Timeout fetching {Url} after {Timeout}s", url, timeoutSeconds);
            return (url, false, string.Empty);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error fetching {Url}: {Message}", url, ex.Message);
            return (url, false, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching {Url}: {Message}", url, ex.Message);
            return (url, false, string.Empty);
        }
    }

    private string ExtractTextFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Remove script, style, and other non-content elements
            var nodesToRemove = doc.DocumentNode.SelectNodes("//script|//style|//nav|//header|//footer|//iframe|//noscript");
            if (nodesToRemove != null)
            {
                foreach (var node in nodesToRemove)
                {
                    node.Remove();
                }
            }

            // Try to find main content area first
            var mainContent = doc.DocumentNode.SelectSingleNode("//main") 
                           ?? doc.DocumentNode.SelectSingleNode("//article") 
                           ?? doc.DocumentNode.SelectSingleNode("//div[@id='content']")
                           ?? doc.DocumentNode.SelectSingleNode("//div[@class='content']")
                           ?? doc.DocumentNode.SelectSingleNode("//body")
                           ?? doc.DocumentNode;

            // Extract text with some structure preservation
            var textBuilder = new System.Text.StringBuilder();
            ExtractTextRecursive(mainContent, textBuilder);

            var text = textBuilder.ToString();

            // Normalize whitespace
            text = Regex.Replace(text, @"\s+", " ");
            text = text.Trim();

            // Limit length
            if (text.Length > MaxContentLength)
            {
                text = text.Substring(0, MaxContentLength);
            }

            return text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing HTML with HtmlAgilityPack, falling back to regex");
            
            // Fallback to simple regex-based extraction
            return ExtractTextWithRegex(html);
        }
    }

    private void ExtractTextRecursive(HtmlNode node, System.Text.StringBuilder textBuilder)
    {
        if (node == null) return;

        // If it's a text node, add its content
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText);
            if (!string.IsNullOrWhiteSpace(text))
            {
                textBuilder.Append(text.Trim()).Append(' ');
            }
            return;
        }

        // Add line breaks for block elements
        if (node.NodeType == HtmlNodeType.Element)
        {
            var blockElements = new[] { "p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "li", "br", "tr" };
            bool isBlockElement = blockElements.Contains(node.Name.ToLower());

            // Process child nodes
            foreach (var child in node.ChildNodes)
            {
                ExtractTextRecursive(child, textBuilder);
            }

            // Add spacing after block elements
            if (isBlockElement)
            {
                textBuilder.Append(' ');
            }
        }
    }

    private string ExtractTextWithRegex(string html)
    {
        // Fallback regex-based extraction
        html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<!--[\s\S]*?-->", "");
        html = Regex.Replace(html, @"<[^>]+>", " ");
        html = System.Net.WebUtility.HtmlDecode(html);
        html = Regex.Replace(html, @"\s+", " ");
        html = html.Trim();
        
        if (html.Length > MaxContentLength)
        {
            html = html.Substring(0, MaxContentLength);
        }
        
        return html;
    }
}
