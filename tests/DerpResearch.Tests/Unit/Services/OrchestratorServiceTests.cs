using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DeepResearch.WebApp.Services;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DerpResearch.Tests.Unit.Services;

public class OrchestratorServiceTests
{
    private readonly Mock<IPlannerAgent> _mockPlannerAgent;
    private readonly Mock<ISearchAgent> _mockSearchAgent;
    private readonly Mock<IMemoryService> _mockMemoryService;
    private readonly Mock<ILLMService> _mockLLMService;
    private readonly Mock<IProgressStreamingService> _mockProgressStreaming;
    private readonly Mock<IClarificationManager> _mockClarificationManager;
    private readonly Mock<IIterativeResearchManager> _mockIterativeResearchManager;
    private readonly Mock<ILogger<OrchestratorService>> _mockLogger;

    public OrchestratorServiceTests()
    {
        _mockPlannerAgent = TestMockFactory.CreatePlannerAgent();
        _mockSearchAgent = new Mock<ISearchAgent>();
        _mockMemoryService = TestMockFactory.CreateMemoryService();
        _mockLLMService = TestMockFactory.CreateLLMService();
        _mockProgressStreaming = new Mock<IProgressStreamingService>();
        _mockClarificationManager = new Mock<IClarificationManager>();
        _mockIterativeResearchManager = new Mock<IIterativeResearchManager>();
        _mockLogger = TestMockFactory.CreateLogger<OrchestratorService>();

        SetupDefaults();
    }

    private void SetupDefaults()
    {
        _mockProgressStreaming.Setup(p => p.CreateProgressToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>()))
            .Returns<string, string, string, object?>((c, s, m, d) => $"[PROGRESS:{s}]");
        
        _mockProgressStreaming.Setup(p => p.CreatePlanToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
            .Returns<string, string, string[]>((c, g, s) => $"[PLAN:{g}]");
        
        _mockProgressStreaming.Setup(p => p.CreateSearchQueryToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns<string, string, int, int>((c, q, n, t) => $"[QUERY:{q}]");
        
        _mockProgressStreaming.Setup(p => p.CreateSourceToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns<string, string, string, string?>((c, t, u, s) => $"[SOURCE:{t}]");
        
        _mockProgressStreaming.Setup(p => p.CreateClarificationToken(
                It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<string>()))
            .Returns<string, string[], string>((c, q, r) => $"[CLARIFICATION]");

        _mockClarificationManager.Setup(c => c.EnhancePromptWithClarificationsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("enhanced prompt");

        _mockIterativeResearchManager.Setup(i => i.ExecuteIterativeResearchAsync(
                It.IsAny<string>(),
                It.IsAny<ResearchPlan>(),
                It.IsAny<GatheredInformation>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(TestMockFactory.CreateAsyncEnumerable(new[] { "result1", "result2" }));
    }

    private OrchestratorService CreateService()
    {
        return new OrchestratorService(
            _mockPlannerAgent.Object,
            _mockSearchAgent.Object,
            _mockMemoryService.Object,
            _mockLLMService.Object,
            _mockProgressStreaming.Object,
            _mockClarificationManager.Object,
            _mockIterativeResearchManager.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessDeepResearchAsync_SavesUserMessage()
    {
        // Arrange
        var service = CreateService();
        SetupSearchAgentWithResults();
        
        _mockClarificationManager.Setup(c => c.GenerateClarificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClarificationResult?)null);

        // Act
        await foreach (var _ in service.ProcessDeepResearchAsync(
            "test prompt", "conv-123", 100, new[] { "answer" }))
        { }

        // Assert
        _mockMemoryService.Verify(m => m.SaveMessageAsync(
            "conv-123",
            "user",
            "test prompt",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDeepResearchAsync_GeneratesClarificationQuestionsWithoutAnswers()
    {
        // Arrange
        var service = CreateService();
        var clarification = TestDataBuilder.CreateClarificationResult();
        
        _mockClarificationManager.Setup(c => c.GenerateClarificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(clarification);

        // Act
        var tokens = new List<string>();
        await foreach (var token in service.ProcessDeepResearchAsync(
            "test prompt", "conv-123", 100, null))
        {
            tokens.Add(token);
        }

        // Assert
        tokens.Should().Contain(t => t.Contains("CLARIFICATION"));
    }

    [Fact]
    public async Task ProcessDeepResearchAsync_SkipsClarificationWithAnswers()
    {
        // Arrange
        var service = CreateService();
        SetupSearchAgentWithResults();

        // Act
        await foreach (var _ in service.ProcessDeepResearchAsync(
            "test prompt", "conv-123", 100, new[] { "answer1" }))
        { }

        // Assert
        _mockClarificationManager.Verify(c => c.EnhancePromptWithClarificationsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDeepResearchAsync_CreatesPlan()
    {
        // Arrange
        var service = CreateService();
        SetupSearchAgentWithResults();

        // Act
        var tokens = new List<string>();
        await foreach (var token in service.ProcessDeepResearchAsync(
            "test prompt", "conv-123", 100, new[] { "answer" }))
        {
            tokens.Add(token);
        }

        // Assert
        _mockPlannerAgent.Verify(p => p.CreatePlanAsync(
            "enhanced prompt",
            It.IsAny<ConversationContext>(),
            100,
            It.IsAny<CancellationToken>()), Times.Once);
        tokens.Should().Contain(t => t.Contains("PLAN"));
    }

    [Fact]
    public async Task ProcessDeepResearchAsync_ExecutesSearch()
    {
        // Arrange
        var service = CreateService();
        SetupSearchAgentWithResults();

        // Act
        await foreach (var _ in service.ProcessDeepResearchAsync(
            "test prompt", "conv-123", 100, new[] { "answer" }))
        { }

        // Assert
        _mockSearchAgent.Verify(s => s.ExecuteSearchPlanAsync(
            It.IsAny<ResearchPlan>(),
            100,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDeepResearchAsync_FallbacksWithNoSources()
    {
        // Arrange
        var service = CreateService();
        SetupSearchAgentWithNoResults();

        // Act
        var tokens = new List<string>();
        await foreach (var token in service.ProcessDeepResearchAsync(
            "test prompt", "conv-123", 100, new[] { "answer" }))
        {
            tokens.Add(token);
        }

        // Assert
        tokens.Should().Contain(t => t.Contains("fallback"));
        _mockLLMService.Verify(l => l.ChatCompletionStream(
            It.IsAny<ChatMessage[]>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDeepResearchAsync_UsesIterativeResearchWithSources()
    {
        // Arrange
        var service = CreateService();
        SetupSearchAgentWithResults();

        // Act
        await foreach (var _ in service.ProcessDeepResearchAsync(
            "test prompt", "conv-123", 100, new[] { "answer" }))
        { }

        // Assert
        _mockIterativeResearchManager.Verify(i => i.ExecuteIterativeResearchAsync(
            It.IsAny<string>(),
            It.IsAny<ResearchPlan>(),
            It.IsAny<GatheredInformation>(),
            "conv-123",
            100,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDeepResearchAsync_SupportsCancellation()
    {
        // Arrange
        var service = CreateService();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in service.ProcessDeepResearchAsync(
                "test prompt", "conv-123", 100, null, cts.Token))
            { }
        });
    }

    [Fact]
    public async Task ProcessDeepResearchAsync_StreamsSearchQueries()
    {
        // Arrange
        var service = CreateService();
        var plan = TestDataBuilder.CreateResearchPlan(subtaskCount: 3);
        _mockPlannerAgent.Setup(p => p.CreatePlanAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationContext>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        SetupSearchAgentWithResults();

        // Act
        var tokens = new List<string>();
        await foreach (var token in service.ProcessDeepResearchAsync(
            "test prompt", "conv-123", 100, new[] { "answer" }))
        {
            tokens.Add(token);
        }

        // Assert
        tokens.Count(t => t.Contains("QUERY")).Should().Be(3);
    }

    [Fact]
    public async Task ProcessDeepResearchAsync_StreamsSources()
    {
        // Arrange
        var service = CreateService();
        
        var results = TestDataBuilder.CreateSearchResults(2);
        var info = new GatheredInformation
        {
            Results = results.ToArray(),
            StoredMemoryIds = new[] { "mem-1" },
            TotalSourcesFound = 2
        };

        _mockSearchAgent.Setup(s => s.ExecuteSearchPlanAsync(
                It.IsAny<ResearchPlan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateSearchResults(results, info));

        // Act
        var tokens = new List<string>();
        await foreach (var token in service.ProcessDeepResearchAsync(
            "test prompt", "conv-123", 100, new[] { "answer" }))
        {
            tokens.Add(token);
        }

        // Assert
        tokens.Count(t => t.Contains("SOURCE")).Should().Be(2);
    }

    private void SetupSearchAgentWithResults()
    {
        var results = TestDataBuilder.CreateSearchResults(3);
        var info = new GatheredInformation
        {
            Results = results.ToArray(),
            StoredMemoryIds = new[] { "mem-1" },
            TotalSourcesFound = 3
        };

        _mockSearchAgent.Setup(s => s.ExecuteSearchPlanAsync(
                It.IsAny<ResearchPlan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateSearchResults(results, info));
    }

    private void SetupSearchAgentWithNoResults()
    {
        var info = new GatheredInformation
        {
            Results = Array.Empty<SearchResult>(),
            StoredMemoryIds = Array.Empty<string>(),
            TotalSourcesFound = 0
        };

        _mockSearchAgent.Setup(s => s.ExecuteSearchPlanAsync(
                It.IsAny<ResearchPlan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateSearchResults(new List<SearchResult>(), info));
    }

    private static async IAsyncEnumerable<object> CreateSearchResults(
        List<SearchResult> results, 
        GatheredInformation info)
    {
        foreach (var result in results)
        {
            await Task.Yield();
            yield return result;
        }
        yield return info;
    }
}
