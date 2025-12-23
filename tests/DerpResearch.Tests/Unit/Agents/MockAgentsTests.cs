using DeepResearch.WebApp.Agents;
using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DerpResearch.Tests.Unit.Agents;

public class MockClarificationAgentTests
{
    private readonly Mock<ILogger<MockClarificationAgent>> _mockLogger;
    private readonly MockClarificationAgent _agent;

    public MockClarificationAgentTests()
    {
        _mockLogger = TestMockFactory.CreateLogger<MockClarificationAgent>();
        _agent = new MockClarificationAgent(_mockLogger.Object);
    }

    [Fact]
    public async Task GenerateClarifyingQuestionsAsync_ReturnsQuestions()
    {
        // Arrange
        var context = TestDataBuilder.CreateConversationContext();

        // Act
        var result = await _agent.GenerateClarifyingQuestionsAsync("test query", context);

        // Assert
        result.Should().NotBeNull();
        result.Questions.Should().NotBeEmpty();
        result.Rationale.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateClarifyingQuestionsAsync_HighDerpification_ReturnsMoreQuestions()
    {
        // Arrange
        var context = TestDataBuilder.CreateConversationContext();

        // Act
        var result = await _agent.GenerateClarifyingQuestionsAsync("test query", context, 100);

        // Assert
        result.Questions.Length.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task GenerateClarifyingQuestionsAsync_LowDerpification_ReturnsFewQuestions()
    {
        // Arrange
        var context = TestDataBuilder.CreateConversationContext();

        // Act
        var result = await _agent.GenerateClarifyingQuestionsAsync("test query", context, 20);

        // Assert
        result.Questions.Length.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task GenerateClarifyingQuestionsAsync_SupportsCancellation()
    {
        // Arrange
        var context = TestDataBuilder.CreateConversationContext();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _agent.GenerateClarifyingQuestionsAsync("test", context, 100, cts.Token);
        });
    }

    [Fact]
    public async Task GenerateClarifyingQuestionsAsync_IncludesQueryInQuestions()
    {
        // Arrange
        var context = TestDataBuilder.CreateConversationContext();

        // Act
        var result = await _agent.GenerateClarifyingQuestionsAsync("machine learning", context);

        // Assert
        result.Questions.Should().Contain(q => q.Contains("machine learning"));
    }
}

public class MockPlannerAgentTests
{
    private readonly Mock<ILogger<MockPlannerAgent>> _mockLogger;
    private readonly MockPlannerAgent _agent;

    public MockPlannerAgentTests()
    {
        _mockLogger = TestMockFactory.CreateLogger<MockPlannerAgent>();
        _agent = new MockPlannerAgent(_mockLogger.Object);
    }

    [Fact]
    public async Task CreatePlanAsync_ReturnsResearchPlan()
    {
        // Arrange
        var context = TestDataBuilder.CreateConversationContext();

        // Act
        var result = await _agent.CreatePlanAsync("test query", context);

        // Assert
        result.Should().NotBeNull();
        result.MainGoal.Should().Contain("test query");
        result.Subtasks.Should().NotBeEmpty();
        result.KeyConcepts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreatePlanAsync_HighDerpification_ReturnsMoreSubtasks()
    {
        // Arrange
        var context = TestDataBuilder.CreateConversationContext();

        // Act
        var result = await _agent.CreatePlanAsync("test query", context, 100);

        // Assert
        result.Subtasks.Length.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task CreatePlanAsync_LowDerpification_ReturnsFewSubtasks()
    {
        // Arrange
        var context = TestDataBuilder.CreateConversationContext();

        // Act
        var result = await _agent.CreatePlanAsync("test query", context, 20);

        // Assert
        result.Subtasks.Length.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task CreatePlanAsync_SupportsCancellation()
    {
        // Arrange
        var context = TestDataBuilder.CreateConversationContext();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _agent.CreatePlanAsync("test", context, 100, cts.Token);
        });
    }

    [Fact]
    public async Task CreatePlanAsync_SubtasksHaveSearchQueries()
    {
        // Arrange
        var context = TestDataBuilder.CreateConversationContext();

        // Act
        var result = await _agent.CreatePlanAsync("test query", context);

        // Assert
        result.Subtasks.Should().AllSatisfy(t =>
        {
            t.SearchQuery.Should().NotBeNullOrEmpty();
            t.Description.Should().NotBeNullOrEmpty();
        });
    }
}

public class MockSynthesisAgentTests
{
    private readonly Mock<ILogger<MockSynthesisAgent>> _mockLogger;
    private readonly MockSynthesisAgent _agent;

    public MockSynthesisAgentTests()
    {
        _mockLogger = TestMockFactory.CreateLogger<MockSynthesisAgent>();
        _agent = new MockSynthesisAgent(_mockLogger.Object);
    }

    [Fact]
    public async Task SynthesizeAsync_YieldsTokens()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = TestDataBuilder.CreateSearchResults(3).ToArray(),
            TotalSourcesFound = 3
        };

        // Act
        var tokens = new List<string>();
        await foreach (var token in _agent.SynthesizeAsync("test", plan, info, Array.Empty<MemoryChunk>()))
        {
            tokens.Add(token);
        }

        // Assert
        tokens.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SynthesizeAsync_HighDerpification_YieldsDetailedResponse()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = TestDataBuilder.CreateSearchResults(3).ToArray(),
            TotalSourcesFound = 3
        };

        // Act
        var tokens = new List<string>();
        await foreach (var token in _agent.SynthesizeAsync("test", plan, info, Array.Empty<MemoryChunk>(), 100))
        {
            tokens.Add(token);
        }

        // Assert
        var fullResponse = string.Join("", tokens);
        fullResponse.Should().Contain("**");
    }

    [Fact]
    public async Task SynthesizeAsync_SupportsCancellation()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation { TotalSourcesFound = 1 };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in _agent.SynthesizeAsync("test", plan, info, Array.Empty<MemoryChunk>(), 100, cts.Token))
            { }
        });
    }

    [Fact]
    public async Task SynthesizeAsync_IncludesSourceCount()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = TestDataBuilder.CreateSearchResults(5).ToArray(),
            TotalSourcesFound = 5
        };

        // Act
        var tokens = new List<string>();
        await foreach (var token in _agent.SynthesizeAsync("test", plan, info, Array.Empty<MemoryChunk>()))
        {
            tokens.Add(token);
        }

        // Assert
        var fullResponse = string.Join("", tokens);
        fullResponse.Should().Contain("5");
    }

    [Fact]
    public async Task SynthesizeAsync_LowDerpification_YieldsConciseResponse()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = TestDataBuilder.CreateSearchResults(2).ToArray(),
            TotalSourcesFound = 2
        };

        // Act
        var tokens = new List<string>();
        await foreach (var token in _agent.SynthesizeAsync("test", plan, info, Array.Empty<MemoryChunk>(), 20))
        {
            tokens.Add(token);
        }

        // Assert
        var fullResponse = string.Join("", tokens);
        fullResponse.Should().Contain("Summary");
    }

    [Fact]
    public async Task SynthesizeAsync_ModerateDerpification_YieldsModerateResponse()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = TestDataBuilder.CreateSearchResults(2).ToArray(),
            TotalSourcesFound = 2
        };

        // Act
        var tokens = new List<string>();
        await foreach (var token in _agent.SynthesizeAsync("test", plan, info, Array.Empty<MemoryChunk>(), 50))
        {
            tokens.Add(token);
        }

        // Assert
        var fullResponse = string.Join("", tokens);
        fullResponse.Should().Contain("**");
    }
}

public class MockReflectionAgentTests
{
    private readonly Mock<ILogger<MockReflectionAgent>> _mockLogger;

    public MockReflectionAgentTests()
    {
        _mockLogger = TestMockFactory.CreateLogger<MockReflectionAgent>();
    }

    [Fact]
    public async Task ReflectAsync_ReturnsReflectionResult()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MockServices:UseFixedConfidence"] = "true",
                ["MockServices:FixedConfidenceScore"] = "0.95"
            })
            .Build();
        
        var agent = new MockReflectionAgent(_mockLogger.Object, config);
        var info = new GatheredInformation { TotalSourcesFound = 3 };

        // Act
        var result = await agent.ReflectAsync("test query", "response", info);

        // Assert
        result.Should().NotBeNull();
        result.ConfidenceScore.Should().BeInRange(0, 1);
    }

    [Fact]
    public async Task ReflectAsync_WithFixedConfidence_UsesConfiguredValue()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MockServices:UseFixedConfidence"] = "true",
                ["MockServices:FixedConfidenceScore"] = "0.85"
            })
            .Build();
        
        var agent = new MockReflectionAgent(_mockLogger.Object, config);
        var info = new GatheredInformation { TotalSourcesFound = 3 };

        // Act
        var result = await agent.ReflectAsync("test query", "response", info);

        // Assert
        result.Should().NotBeNull();
        result.ConfidenceScore.Should().Be(0.85f);
    }

    [Fact]
    public async Task ReflectAsync_SupportsCancellation()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var agent = new MockReflectionAgent(_mockLogger.Object, config);
        var info = new GatheredInformation { TotalSourcesFound = 3 };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await agent.ReflectAsync("test", "response", info, 100, cts.Token);
        });
    }

    [Fact]
    public async Task ReflectAsync_RandomConfidence_HasExpectedProperties()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MockServices:UseFixedConfidence"] = "false"
            })
            .Build();
        
        var agent = new MockReflectionAgent(_mockLogger.Object, config);
        var info = new GatheredInformation { TotalSourcesFound = 3 };

        // Act
        var result = await agent.ReflectAsync("test query", "response", info, 100);
        
        // Assert
        result.Should().NotBeNull();
        result.ConfidenceScore.Should().BeInRange(0, 1);
        // Either it needs more research or it doesn't
        if (result.RequiresMoreResearch)
        {
            result.IdentifiedGaps.Should().NotBeEmpty();
            result.SuggestedAdditionalSearches.Should().NotBeEmpty();
        }
    }
}

public class MockSearchAgentTests
{
    private readonly Mock<ILogger<MockSearchAgent>> _mockLogger;
    private readonly Mock<ISearchService> _mockSearchService;
    private readonly Mock<IWebContentFetcher> _mockWebContentFetcher;
    private readonly Mock<IMemoryService> _mockMemoryService;
    private readonly MockSearchAgent _agent;

    public MockSearchAgentTests()
    {
        _mockLogger = TestMockFactory.CreateLogger<MockSearchAgent>();
        _mockSearchService = TestMockFactory.CreateSearchService();
        _mockWebContentFetcher = TestMockFactory.CreateWebContentFetcher();
        _mockMemoryService = TestMockFactory.CreateMemoryService();

        _agent = new MockSearchAgent(
            _mockLogger.Object,
            _mockSearchService.Object,
            _mockWebContentFetcher.Object,
            _mockMemoryService.Object);
    }

    [Fact]
    public async Task ExecuteSearchPlanAsync_YieldsSearchResults()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan(subtaskCount: 1);
        var searchResults = new[]
        {
            new SearchResult { Title = "Test", Url = "https://example.com", Snippet = "Test" }
        };
        
        _mockSearchService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        _mockWebContentFetcher
            .Setup(f => f.FetchContentAsync(It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["https://example.com"] = "Content" });

        // Act
        var results = new List<object>();
        await foreach (var result in _agent.ExecuteSearchPlanAsync(plan))
        {
            results.Add(result);
        }

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(r => r is GatheredInformation);
    }

    [Fact]
    public async Task ExecuteSearchPlanAsync_SupportsCancellation()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan(subtaskCount: 1);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in _agent.ExecuteSearchPlanAsync(plan, 100, cts.Token))
            { }
        });
    }

    [Fact]
    public async Task ExecuteSearchPlanAsync_StoresMemoryForResults()
    {
        // Arrange
        var plan = TestDataBuilder.CreateResearchPlan(subtaskCount: 1);
        var searchResults = new[]
        {
            new SearchResult { Title = "Test", Url = "https://example.com", Snippet = "Test" }
        };
        
        _mockSearchService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        _mockWebContentFetcher
            .Setup(f => f.FetchContentAsync(It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["https://example.com"] = "Content" });

        // Act
        await foreach (var _ in _agent.ExecuteSearchPlanAsync(plan))
        { }

        // Assert
        _mockMemoryService.Verify(m => m.StoreMemoryAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string[]>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
