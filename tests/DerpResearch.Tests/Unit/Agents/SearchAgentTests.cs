using DeepResearch.WebApp.Agents;
using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DerpResearch.Tests.Unit.Agents;

public class SearchAgentTests
{
    private readonly Mock<ISearchService> _mockSearchService;
    private readonly Mock<IMemoryService> _mockMemoryService;
    private readonly Mock<IWebContentFetcher> _mockContentFetcher;
    private readonly Mock<ILogger<SearchAgent>> _mockLogger;
    private readonly SearchAgent _agent;

    public SearchAgentTests()
    {
        _mockSearchService = TestMockFactory.CreateSearchService();
        _mockMemoryService = TestMockFactory.CreateMemoryService();
        _mockContentFetcher = TestMockFactory.CreateWebContentFetcher();
        _mockLogger = TestMockFactory.CreateLogger<SearchAgent>();
        
        _agent = new SearchAgent(
            _mockSearchService.Object,
            _mockMemoryService.Object,
            _mockContentFetcher.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteSearchPlanAsync_WithValidPlan_YieldsResultsAndInfo()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan(subtaskCount: 2);
        var searchResults = TestDataBuilder.CreateSearchResults(3).ToArray();
        var fetchedContent = new Dictionary<string, string>
        {
            ["https://example.com/article1"] = "Content 1",
            ["https://example.com/article2"] = "Content 2",
            ["https://example.com/article3"] = "Content 3"
        };

        _mockSearchService.Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        _mockContentFetcher.Setup(f => f.FetchContentAsync(
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(fetchedContent);

        _mockMemoryService.Setup(m => m.StoreMemoryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateStoreMemoryResult());

        // Act
        var results = new List<object>();
        await foreach (var item in _agent.ExecuteSearchPlanAsync(plan))
        {
            results.Add(item);
        }

        // Assert
        results.Should().NotBeEmpty();
        results.OfType<SearchResult>().Should().NotBeEmpty();
        results.OfType<GatheredInformation>().Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteSearchPlanAsync_WithNoResults_ReturnsEmptyInfo()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan();

        _mockSearchService.Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SearchResult>());

        // Act
        var results = new List<object>();
        await foreach (var item in _agent.ExecuteSearchPlanAsync(plan))
        {
            results.Add(item);
        }

        // Assert
        var info = results.OfType<GatheredInformation>().Single();
        info.TotalSourcesFound.Should().Be(0);
        info.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteSearchPlanAsync_WhenSearchFails_ContinuesWithOtherTasks()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan(subtaskCount: 3);
        var callCount = 0;

        _mockSearchService.Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new Exception("Search failed");
                return Task.FromResult(TestDataBuilder.CreateSearchResults(2).ToArray());
            });

        _mockContentFetcher.Setup(f => f.FetchContentAsync(
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["https://example.com/article1"] = "Content"
            });

        _mockMemoryService.Setup(m => m.StoreMemoryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateStoreMemoryResult());

        // Act
        var results = new List<object>();
        await foreach (var item in _agent.ExecuteSearchPlanAsync(plan))
        {
            results.Add(item);
        }

        // Assert
        var info = results.OfType<GatheredInformation>().Single();
        info.TotalSourcesFound.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteSearchPlanAsync_WhenContentFetchFails_ContinuesWithOtherTasks()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan(subtaskCount: 2);
        var callCount = 0;

        _mockSearchService.Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateSearchResults(2).ToArray());

        _mockContentFetcher.Setup(f => f.FetchContentAsync(
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new Exception("Fetch failed");
                return Task.FromResult(new Dictionary<string, string>
                {
                    ["https://example.com/article1"] = "Content"
                });
            });

        _mockMemoryService.Setup(m => m.StoreMemoryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateStoreMemoryResult());

        // Act
        var results = new List<object>();
        await foreach (var item in _agent.ExecuteSearchPlanAsync(plan))
        {
            results.Add(item);
        }

        // Assert
        var info = results.OfType<GatheredInformation>().Single();
        info.Should().NotBeNull();
    }

    [Theory]
    [InlineData(0, 3)]    // Derp mode: fewer results
    [InlineData(33, 3)]   // Edge of derp mode
    [InlineData(50, 5)]   // Average mode
    [InlineData(66, 5)]   // Edge of average mode
    [InlineData(100, 8)]  // Smart mode: more results
    public async Task ExecuteSearchPlanAsync_DerpificationLevel_AffectsResultCount(int derpLevel, int expectedResults)
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan(subtaskCount: 1);

        _mockSearchService.Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateSearchResults(expectedResults).ToArray());

        _mockContentFetcher.Setup(f => f.FetchContentAsync(
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        // Act
        await foreach (var _ in _agent.ExecuteSearchPlanAsync(plan, derpLevel)) { }

        // Assert
        _mockSearchService.Verify(s => s.SearchAsync(
            It.IsAny<string>(),
            expectedResults,
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteSearchPlanAsync_StoresResultsInMemory()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan(subtaskCount: 1);
        var searchResults = new[]
        {
            new SearchResult { Title = "Article 1", Url = "https://example.com/1", Snippet = "Snippet 1" },
            new SearchResult { Title = "Article 2", Url = "https://example.com/2", Snippet = "Snippet 2" }
        };
        var fetchedContent = new Dictionary<string, string>
        {
            ["https://example.com/1"] = "Full content 1",
            ["https://example.com/2"] = "Full content 2"
        };

        _mockSearchService.Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        _mockContentFetcher.Setup(f => f.FetchContentAsync(
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(fetchedContent);

        _mockMemoryService.Setup(m => m.StoreMemoryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateStoreMemoryResult());

        // Act
        await foreach (var _ in _agent.ExecuteSearchPlanAsync(plan)) { }

        // Assert
        _mockMemoryService.Verify(m => m.StoreMemoryAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string[]>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteSearchPlanAsync_WhenMemoryStoreFails_ContinuesWithOtherResults()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan(subtaskCount: 1);
        var searchResults = TestDataBuilder.CreateSearchResults(2).ToArray();
        var fetchedContent = new Dictionary<string, string>
        {
            [searchResults[0].Url] = "Content 1",
            [searchResults[1].Url] = "Content 2"
        };

        _mockSearchService.Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        _mockContentFetcher.Setup(f => f.FetchContentAsync(
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(fetchedContent);

        var callCount = 0;
        _mockMemoryService.Setup(m => m.StoreMemoryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First call returns a complete failure result
                    return Task.FromResult(TestDataBuilder.CreateFailedStoreMemoryResult());
                }
                return Task.FromResult(TestDataBuilder.CreateStoreMemoryResult());
            });

        // Act
        var results = new List<object>();
        await foreach (var item in _agent.ExecuteSearchPlanAsync(plan))
        {
            results.Add(item);
        }

        // Assert
        var info = results.OfType<GatheredInformation>().Single();
        info.StoredMemoryIds.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteSearchPlanAsync_RespectsCancellationToken()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in _agent.ExecuteSearchPlanAsync(plan, cancellationToken: cts.Token)) { }
        });
    }
}
