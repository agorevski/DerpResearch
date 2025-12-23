using DeepResearch.WebApp.Services;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DerpResearch.Tests.Unit.Services;

public class MockWebContentFetcherTests
{
    private readonly Mock<ILogger<MockWebContentFetcher>> _mockLogger;
    private readonly MockWebContentFetcher _fetcher;

    public MockWebContentFetcherTests()
    {
        _mockLogger = TestMockFactory.CreateLogger<MockWebContentFetcher>();
        _fetcher = new MockWebContentFetcher(_mockLogger.Object);
    }

    [Fact]
    public async Task FetchContentAsync_ReturnsContentForAllUrls()
    {
        // Arrange
        var urls = new[] 
        { 
            "https://example.com/article1",
            "https://example.com/article2" 
        };

        // Act
        var results = await _fetcher.FetchContentAsync(urls);

        // Assert
        results.Should().HaveCount(2);
        results.Should().ContainKeys(urls);
    }

    [Fact]
    public async Task FetchContentAsync_ReturnsNonEmptyContent()
    {
        // Arrange
        var urls = new[] { "https://example.com/test" };

        // Act
        var results = await _fetcher.FetchContentAsync(urls);

        // Assert
        results["https://example.com/test"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_ExtractsTopicFromQueryString()
    {
        // Arrange
        var urls = new[] { "https://example.com?q=machine+learning" };

        // Act
        var results = await _fetcher.FetchContentAsync(urls);

        // Assert
        var content = results.Values.First();
        // The URL-encoded topic may appear with + or spaces depending on decoding
        content.ToLower().Should().Match(c => c.Contains("machine") || c.Contains("learning"));
    }

    [Fact]
    public async Task FetchContentAsync_ExtractsTopicFromPath()
    {
        // Arrange
        var urls = new[] { "https://example.com/articles/deep-learning" };

        // Act
        var results = await _fetcher.FetchContentAsync(urls);

        // Assert
        var content = results.Values.First();
        content.Should().Contain("deep learning");
    }

    [Fact]
    public async Task FetchContentAsync_SupportsCancellation()
    {
        // Arrange
        var urls = new[] { "https://example.com/test" };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _fetcher.FetchContentAsync(urls, cancellationToken: cts.Token);
        });
    }

    [Fact]
    public async Task FetchContentAsync_GeneratesVariedContent()
    {
        // Arrange
        var urls = Enumerable.Range(1, 10)
            .Select(i => $"https://example.com/article{i}")
            .ToArray();

        // Act
        var results = await _fetcher.FetchContentAsync(urls);

        // Assert
        var uniqueContentCount = results.Values.Distinct().Count();
        uniqueContentCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task FetchContentAsync_ContentContainsStructuredElements()
    {
        // Arrange
        var urls = new[] { "https://example.com/test" };

        // Act
        var results = await _fetcher.FetchContentAsync(urls);

        // Assert
        var content = results.Values.First();
        content.Should().Contain("#");
    }

    [Fact]
    public async Task FetchContentAsync_HandlesEmptyUrlArray()
    {
        // Act
        var results = await _fetcher.FetchContentAsync(Array.Empty<string>());

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_RespectsTimeoutParameter()
    {
        // Arrange
        var urls = new[] { "https://example.com/test" };

        // Act
        var act = async () => await _fetcher.FetchContentAsync(urls, timeoutSeconds: 1);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task FetchContentAsync_ContentContainsTopicReferences()
    {
        // Arrange
        var urls = new[] { "https://example.com?q=artificial%20intelligence" };

        // Act
        var results = await _fetcher.FetchContentAsync(urls);

        // Assert
        var content = results.Values.First();
        // Content may contain the topic with different encodings
        content.ToLower().Should().Match(c => c.Contains("artificial") || c.Contains("intelligence"));
    }

    [Fact]
    public async Task FetchContentAsync_HandlesQueryStringWithAmpersand()
    {
        // Arrange
        var urls = new[] { "https://example.com?q=test&page=1" };

        // Act
        var results = await _fetcher.FetchContentAsync(urls);

        // Assert
        results.Should().HaveCount(1);
        var content = results.Values.First();
        content.Should().Contain("test");
    }

    [Fact]
    public async Task FetchContentAsync_HandlesMalformedUrl()
    {
        // Arrange
        var urls = new[] { "not-a-valid-url" };

        // Act
        var results = await _fetcher.FetchContentAsync(urls);

        // Assert
        results.Should().HaveCount(1);
        results.Values.First().Should().Contain("the topic");
    }
}
