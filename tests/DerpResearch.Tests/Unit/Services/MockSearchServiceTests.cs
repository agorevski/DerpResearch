using DeepResearch.WebApp.Services;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DerpResearch.Tests.Unit.Services;

public class MockSearchServiceTests
{
    private readonly Mock<ILogger<MockSearchService>> _mockLogger;
    private readonly MockSearchService _service;

    public MockSearchServiceTests()
    {
        _mockLogger = TestMockFactory.CreateLogger<MockSearchService>();
        _service = new MockSearchService(_mockLogger.Object);
    }

    [Fact]
    public async Task SearchAsync_ReturnsMockResults()
    {
        // Act
        var results = await _service.SearchAsync("test query");

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_ResultsContainQueryInTitle()
    {
        // Act
        var results = await _service.SearchAsync("machine learning");

        // Assert
        results.Should().AllSatisfy(r => r.Title.Should().Contain("machine learning"));
    }

    [Fact]
    public async Task SearchAsync_ResultsContainQueryInUrl()
    {
        // Act
        var results = await _service.SearchAsync("test");

        // Assert
        results.Should().AllSatisfy(r => r.Url.Should().Contain("test"));
    }

    [Fact]
    public async Task SearchAsync_ResultsContainQueryInSnippet()
    {
        // Act
        var results = await _service.SearchAsync("AI research");

        // Assert
        results.Should().AllSatisfy(r => r.Snippet.Should().Contain("AI research"));
    }

    [Fact]
    public async Task SearchAsync_RespectsMaxResults()
    {
        // Act
        var results = await _service.SearchAsync("test", maxResults: 10);

        // Assert
        results.Should().HaveCountLessOrEqualTo(2);
    }

    [Fact]
    public async Task SearchAsync_SupportsCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _service.SearchAsync("test", cancellationToken: cts.Token);
        });
    }

    [Fact]
    public async Task ClearExpiredCacheAsync_CompletesSuccessfully()
    {
        // Act & Assert
        var act = async () => await _service.ClearExpiredCacheAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ClearExpiredCacheAsync_SupportsCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _service.ClearExpiredCacheAsync(cts.Token);
        });
    }

    [Fact]
    public async Task SearchAsync_ResultsHaveValidUrls()
    {
        // Act
        var results = await _service.SearchAsync("test");

        // Assert
        results.Should().AllSatisfy(r => 
            Uri.TryCreate(r.Url, UriKind.Absolute, out _).Should().BeTrue());
    }

    [Fact]
    public async Task SearchAsync_ResultsHaveNonEmptySnippets()
    {
        // Act
        var results = await _service.SearchAsync("test");

        // Assert
        results.Should().AllSatisfy(r => r.Snippet.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task SearchAsync_ResultsHaveIncrementingNumbers()
    {
        // Act
        var results = await _service.SearchAsync("test");

        // Assert
        for (int i = 0; i < results.Length; i++)
        {
            results[i].Title.Should().Contain($"Result {i + 1}");
        }
    }
}
