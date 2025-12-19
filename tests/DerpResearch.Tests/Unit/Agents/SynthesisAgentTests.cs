using DeepResearch.WebApp.Agents;
using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DerpResearch.Tests.Unit.Agents;

public class SynthesisAgentTests
{
    private readonly Mock<ILLMService> _mockLLMService;
    private readonly Mock<ILogger<SynthesisAgent>> _mockLogger;
    private readonly SynthesisAgent _agent;

    public SynthesisAgentTests()
    {
        _mockLLMService = TestMockFactory.CreateLLMService();
        _mockLogger = TestMockFactory.CreateLogger<SynthesisAgent>();
        _agent = new SynthesisAgent(_mockLLMService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task SynthesizeAsync_WithValidInput_StreamsTokens()
    {
        // Arrange
        var query = "What is machine learning?";
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = TestDataBuilder.CreateSearchResults(5).ToArray(),
            StoredMemoryIds = new[] { "mem1", "mem2" },
            TotalSourcesFound = 5
        };
        var memories = new[] { TestDataBuilder.CreateMemoryChunk() };
        var expectedTokens = new[] { "Machine ", "learning ", "is..." };

        _mockLLMService.Setup(s => s.ChatCompletionStream(
                It.IsAny<ChatMessage[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(TestMockFactory.CreateAsyncEnumerable(expectedTokens));

        // Act
        var tokens = new List<string>();
        await foreach (var token in _agent.SynthesizeAsync(query, plan, info, memories))
        {
            tokens.Add(token);
        }

        // Assert
        tokens.Should().HaveCount(3);
        tokens[0].Should().Be("Machine ");
    }

    [Theory]
    [InlineData(0)]   // Derp mode
    [InlineData(33)]  // Edge of derp mode
    [InlineData(50)]  // Average mode
    [InlineData(66)]  // Edge of average mode
    [InlineData(100)] // Smart mode
    public async Task SynthesizeAsync_WithDifferentDerpificationLevels_AdjustsStyle(int derpLevel)
    {
        // Arrange
        var query = "Test query";
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation { Results = Array.Empty<SearchResult>(), TotalSourcesFound = 0 };
        var memories = Array.Empty<MemoryChunk>();

        _mockLLMService.Setup(s => s.ChatCompletionStream(
                It.IsAny<ChatMessage[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(TestMockFactory.CreateAsyncEnumerable(new[] { "Response" }));

        // Act
        await foreach (var _ in _agent.SynthesizeAsync(query, plan, info, memories, derpLevel)) { }

        // Assert
        _mockLLMService.Verify(s => s.ChatCompletionStream(
            It.Is<ChatMessage[]>(msgs => derpLevel <= 33 
                ? msgs[1].Content.Contains("SUPER SIMPLE") 
                : derpLevel <= 66 
                    ? msgs[1].Content.Contains("BALANCED") 
                    : msgs[1].Content.Contains("COMPREHENSIVE")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SynthesizeAsync_IncludesSourcesInPrompt()
    {
        // Arrange
        var query = "Test query";
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation
        {
            Results = new[]
            {
                TestDataBuilder.CreateSearchResult("Article 1", "https://example1.com", "Snippet 1"),
                TestDataBuilder.CreateSearchResult("Article 2", "https://example2.com", "Snippet 2")
            },
            TotalSourcesFound = 2
        };
        var memories = Array.Empty<MemoryChunk>();

        _mockLLMService.Setup(s => s.ChatCompletionStream(
                It.IsAny<ChatMessage[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(TestMockFactory.CreateAsyncEnumerable(new[] { "Response" }));

        // Act
        await foreach (var _ in _agent.SynthesizeAsync(query, plan, info, memories)) { }

        // Assert
        _mockLLMService.Verify(s => s.ChatCompletionStream(
            It.Is<ChatMessage[]>(msgs => 
                msgs[1].Content.Contains("[1]") &&
                msgs[1].Content.Contains("[2]") &&
                msgs[1].Content.Contains("Article 1") &&
                msgs[1].Content.Contains("https://example1.com")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SynthesizeAsync_IncludesRelevantMemoriesInPrompt()
    {
        // Arrange
        var query = "Test query";
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation { Results = Array.Empty<SearchResult>(), TotalSourcesFound = 0 };
        var memories = new[]
        {
            TestDataBuilder.CreateMemoryChunk("Previous research on ML"),
            TestDataBuilder.CreateMemoryChunk("Context from earlier conversation")
        };

        _mockLLMService.Setup(s => s.ChatCompletionStream(
                It.IsAny<ChatMessage[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(TestMockFactory.CreateAsyncEnumerable(new[] { "Response" }));

        // Act
        await foreach (var _ in _agent.SynthesizeAsync(query, plan, info, memories)) { }

        // Assert
        _mockLLMService.Verify(s => s.ChatCompletionStream(
            It.Is<ChatMessage[]>(msgs => 
                msgs[1].Content.Contains("Relevant Context from Previous Research")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SynthesizeAsync_IncludesResearchGoalInPrompt()
    {
        // Arrange
        var query = "Test query";
        var plan = new ResearchPlan
        {
            MainGoal = "Understand machine learning fundamentals",
            Subtasks = Array.Empty<ResearchTask>()
        };
        var info = new GatheredInformation { Results = Array.Empty<SearchResult>(), TotalSourcesFound = 0 };
        var memories = Array.Empty<MemoryChunk>();

        _mockLLMService.Setup(s => s.ChatCompletionStream(
                It.IsAny<ChatMessage[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(TestMockFactory.CreateAsyncEnumerable(new[] { "Response" }));

        // Act
        await foreach (var _ in _agent.SynthesizeAsync(query, plan, info, memories)) { }

        // Assert
        _mockLLMService.Verify(s => s.ChatCompletionStream(
            It.Is<ChatMessage[]>(msgs => 
                msgs[1].Content.Contains("Research Goal: Understand machine learning fundamentals")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SynthesizeAsync_UsesGpt4oDeployment()
    {
        // Arrange
        var query = "Test query";
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation { Results = Array.Empty<SearchResult>(), TotalSourcesFound = 0 };
        var memories = Array.Empty<MemoryChunk>();

        _mockLLMService.Setup(s => s.ChatCompletionStream(
                It.IsAny<ChatMessage[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(TestMockFactory.CreateAsyncEnumerable(new[] { "Response" }));

        // Act
        await foreach (var _ in _agent.SynthesizeAsync(query, plan, info, memories)) { }

        // Assert
        _mockLLMService.Verify(s => s.ChatCompletionStream(
            It.IsAny<ChatMessage[]>(),
            "gpt-4o",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SynthesizeAsync_RespectsCancellationToken()
    {
        // Arrange
        var query = "Test query";
        var plan = TestDataBuilder.CreateResearchPlan();
        var info = new GatheredInformation { Results = Array.Empty<SearchResult>(), TotalSourcesFound = 0 };
        var memories = Array.Empty<MemoryChunk>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in _agent.SynthesizeAsync(query, plan, info, memories, cancellationToken: cts.Token)) { }
        });
    }
}
