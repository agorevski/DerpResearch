using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DeepResearch.WebApp.Services;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DerpResearch.Tests.Unit.Services;

public class IterativeResearchManagerTests
{
    private readonly Mock<ISynthesisAgent> _mockSynthesisAgent;
    private readonly Mock<IReflectionAgent> _mockReflectionAgent;
    private readonly Mock<ISearchAgent> _mockSearchAgent;
    private readonly Mock<IMemoryService> _mockMemoryService;
    private readonly Mock<IProgressStreamingService> _mockProgressStreaming;
    private readonly Mock<ILogger<IterativeResearchManager>> _mockLogger;
    private readonly IOptions<ReflectionConfiguration> _reflectionOptions;

    public IterativeResearchManagerTests()
    {
        _mockSynthesisAgent = TestMockFactory.CreateSynthesisAgent();
        _mockReflectionAgent = TestMockFactory.CreateReflectionAgent();
        _mockSearchAgent = TestMockFactory.CreateSearchAgent();
        _mockMemoryService = TestMockFactory.CreateMemoryService();
        _mockProgressStreaming = new Mock<IProgressStreamingService>();
        _mockLogger = TestMockFactory.CreateLogger<IterativeResearchManager>();
        
        _reflectionOptions = Options.Create(new ReflectionConfiguration
        {
            MaxIterations = 2,
            ConfidenceThreshold = 0.7
        });

        SetupProgressStreaming();
    }

    private void SetupProgressStreaming()
    {
        _mockProgressStreaming.Setup(p => p.CreateProgressToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>()))
            .Returns<string, string, string, object?>((c, s, m, d) => $"[PROGRESS:{s}:{m}]");
        
        _mockProgressStreaming.Setup(p => p.CreateReflectionToken(
                It.IsAny<string>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns<string, double, string, int>((c, s, r, i) => $"[REFLECTION:{s}:{i}]");
        
        _mockProgressStreaming.Setup(p => p.CreateSearchQueryToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns<string, string, int, int>((c, q, n, t) => $"[QUERY:{q}]");
        
        _mockProgressStreaming.Setup(p => p.CreateSourceToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns<string, string, string, string?>((c, t, u, s) => $"[SOURCE:{t}]");
    }

    private IterativeResearchManager CreateManager()
    {
        return new IterativeResearchManager(
            _mockSynthesisAgent.Object,
            _mockReflectionAgent.Object,
            _mockSearchAgent.Object,
            _mockMemoryService.Object,
            _mockProgressStreaming.Object,
            _reflectionOptions,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteIterativeResearchAsync_YieldsSynthesizedContent()
    {
        // Arrange
        var manager = CreateManager();
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = TestDataBuilder.CreateSearchResults(3).ToArray(),
            StoredMemoryIds = new[] { "mem-1" },
            TotalSourcesFound = 3
        };

        // Act
        var tokens = new List<string>();
        await foreach (var token in manager.ExecuteIterativeResearchAsync(
            "test prompt", plan, info, "conv-123", 100))
        {
            tokens.Add(token);
        }

        // Assert
        tokens.Should().NotBeEmpty();
        tokens.Should().Contain(t => t.Contains("PROGRESS"));
    }

    [Fact]
    public async Task ExecuteIterativeResearchAsync_CallsSynthesisAgent()
    {
        // Arrange
        var manager = CreateManager();
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = Array.Empty<SearchResult>(),
            StoredMemoryIds = Array.Empty<string>(),
            TotalSourcesFound = 0
        };

        // Act
        await foreach (var _ in manager.ExecuteIterativeResearchAsync(
            "test prompt", plan, info, "conv-123", 100))
        { }

        // Assert
        _mockSynthesisAgent.Verify(a => a.SynthesizeAsync(
            It.IsAny<string>(),
            It.IsAny<ResearchPlan>(),
            It.IsAny<GatheredInformation>(),
            It.IsAny<MemoryChunk[]>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteIterativeResearchAsync_CallsReflectionAgent()
    {
        // Arrange
        var manager = CreateManager();
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = Array.Empty<SearchResult>(),
            StoredMemoryIds = Array.Empty<string>(),
            TotalSourcesFound = 0
        };

        // Act
        await foreach (var _ in manager.ExecuteIterativeResearchAsync(
            "test prompt", plan, info, "conv-123", 100))
        { }

        // Assert
        _mockReflectionAgent.Verify(a => a.ReflectAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<GatheredInformation>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteIterativeResearchAsync_SavesAssistantMessage()
    {
        // Arrange
        var manager = CreateManager();
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = Array.Empty<SearchResult>(),
            StoredMemoryIds = Array.Empty<string>(),
            TotalSourcesFound = 0
        };

        // Act
        await foreach (var _ in manager.ExecuteIterativeResearchAsync(
            "test prompt", plan, info, "conv-123", 100))
        { }

        // Assert
        _mockMemoryService.Verify(m => m.SaveMessageAsync(
            "conv-123",
            "assistant",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteIterativeResearchAsync_StoresSynthesisAsMemory()
    {
        // Arrange
        var manager = CreateManager();
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = Array.Empty<SearchResult>(),
            StoredMemoryIds = Array.Empty<string>(),
            TotalSourcesFound = 0
        };

        // Act
        await foreach (var _ in manager.ExecuteIterativeResearchAsync(
            "test prompt", plan, info, "conv-123", 100))
        { }

        // Assert
        _mockMemoryService.Verify(m => m.StoreMemoryAsync(
            It.IsAny<string>(),
            "deep-research-synthesis",
            It.Is<string[]>(tags => tags.Contains("synthesis")),
            "conv-123",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteIterativeResearchAsync_IteratesWhenReflectionRequiresMore()
    {
        // Arrange
        var callCount = 0;
        _mockReflectionAgent
            .Setup(a => a.ReflectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GatheredInformation>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new ReflectionResult
                {
                    ConfidenceScore = callCount == 1 ? 0.5f : 0.9f,
                    RequiresMoreResearch = callCount == 1,
                    IdentifiedGaps = callCount == 1 ? new[] { "Need more data" } : Array.Empty<string>(),
                    SuggestedAdditionalSearches = callCount == 1 ? new[] { "additional query" } : Array.Empty<string>()
                };
            });

        var manager = CreateManager();
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = TestDataBuilder.CreateSearchResults(1).ToArray(),
            StoredMemoryIds = new[] { "mem-1" },
            TotalSourcesFound = 1
        };

        // Act
        await foreach (var _ in manager.ExecuteIterativeResearchAsync(
            "test prompt", plan, info, "conv-123", 100))
        { }

        // Assert
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteIterativeResearchAsync_StopsAtMaxIterations()
    {
        // Arrange
        _mockReflectionAgent
            .Setup(a => a.ReflectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GatheredInformation>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReflectionResult
            {
                ConfidenceScore = 0.3f,
                RequiresMoreResearch = true,
                IdentifiedGaps = new[] { "Gap" },
                SuggestedAdditionalSearches = new[] { "query" }
            });

        var manager = CreateManager();
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = TestDataBuilder.CreateSearchResults(1).ToArray(),
            StoredMemoryIds = new[] { "mem-1" },
            TotalSourcesFound = 1
        };

        // Act
        await foreach (var _ in manager.ExecuteIterativeResearchAsync(
            "test prompt", plan, info, "conv-123", 100))
        { }

        // Assert
        _mockReflectionAgent.Verify(a => a.ReflectAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<GatheredInformation>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteIterativeResearchAsync_YieldsReflectionToken()
    {
        // Arrange
        var manager = CreateManager();
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = Array.Empty<SearchResult>(),
            StoredMemoryIds = Array.Empty<string>(),
            TotalSourcesFound = 0
        };

        // Act
        var tokens = new List<string>();
        await foreach (var token in manager.ExecuteIterativeResearchAsync(
            "test prompt", plan, info, "conv-123", 100))
        {
            tokens.Add(token);
        }

        // Assert
        tokens.Should().Contain(t => t.Contains("REFLECTION"));
    }

    [Fact]
    public async Task ExecuteIterativeResearchAsync_SupportsCancellation()
    {
        // Arrange
        var manager = CreateManager();
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = Array.Empty<SearchResult>(),
            StoredMemoryIds = Array.Empty<string>(),
            TotalSourcesFound = 0
        };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in manager.ExecuteIterativeResearchAsync(
                "test prompt", plan, info, "conv-123", 100, cts.Token))
            { }
        });
    }
}
