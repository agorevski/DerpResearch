using DeepResearch.WebApp.Agents;
using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DerpResearch.Tests.Unit.Agents;

public class PlannerAgentTests
{
    private readonly Mock<ILLMService> _mockLLMService;
    private readonly Mock<ILogger<PlannerAgent>> _mockLogger;
    private readonly PlannerAgent _agent;

    public PlannerAgentTests()
    {
        _mockLLMService = TestMockFactory.CreateLLMService();
        _mockLogger = TestMockFactory.CreateLogger<PlannerAgent>();
        _agent = new PlannerAgent(_mockLLMService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CreatePlanAsync_WithValidQuery_ReturnsResearchPlan()
    {
        // Arrange
        var query = "Compare neural networks and decision trees";
        var context = TestDataBuilder.CreateConversationContext();
        var expectedPlan = TestDataBuilder.CreateResearchPlan();

        _mockLLMService.Setup(s => s.GetStructuredOutput<ResearchPlan>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPlan);

        // Act
        var result = await _agent.CreatePlanAsync(query, context);

        // Assert
        result.Should().NotBeNull();
        result.MainGoal.Should().NotBeNullOrEmpty();
        result.Subtasks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreatePlanAsync_WhenLLMReturnsNull_ReturnsFallbackPlan()
    {
        // Arrange
        var query = "Test query";
        var context = TestDataBuilder.CreateConversationContext();

        _mockLLMService.Setup(s => s.GetStructuredOutput<ResearchPlan>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResearchPlan?)null);

        // Act
        var result = await _agent.CreatePlanAsync(query, context);

        // Assert
        result.Should().NotBeNull();
        result.MainGoal.Should().Be(query);
        result.Subtasks.Should().HaveCount(1);
        result.Subtasks[0].SearchQuery.Should().Be(query);
    }

    [Theory]
    [InlineData(0)]   // Derp mode
    [InlineData(33)]  // Edge of derp mode
    [InlineData(50)]  // Average mode
    [InlineData(66)]  // Edge of average mode
    [InlineData(100)] // Smart mode
    public async Task CreatePlanAsync_WithDifferentDerpificationLevels_AdjustsComplexity(int derpLevel)
    {
        // Arrange
        var query = "Test query";
        var context = TestDataBuilder.CreateConversationContext();
        var plan = TestDataBuilder.CreateResearchPlan();

        _mockLLMService.Setup(s => s.GetStructuredOutput<ResearchPlan>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _agent.CreatePlanAsync(query, context, derpLevel);

        // Assert
        result.Should().NotBeNull();
        _mockLLMService.Verify(s => s.GetStructuredOutput<ResearchPlan>(
            It.Is<string>(prompt => derpLevel <= 33 
                ? prompt.Contains("SUPER SIMPLE") 
                : derpLevel <= 66 
                    ? prompt.Contains("BALANCED") 
                    : prompt.Contains("COMPREHENSIVE")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePlanAsync_SortsSubtasksByPriority()
    {
        // Arrange
        var query = "Test query";
        var context = TestDataBuilder.CreateConversationContext();
        var plan = new ResearchPlan
        {
            MainGoal = "Test goal",
            Subtasks = new[]
            {
                new ResearchTask { Description = "Task 3", SearchQuery = "q3", Priority = 3 },
                new ResearchTask { Description = "Task 1", SearchQuery = "q1", Priority = 1 },
                new ResearchTask { Description = "Task 2", SearchQuery = "q2", Priority = 2 }
            }
        };

        _mockLLMService.Setup(s => s.GetStructuredOutput<ResearchPlan>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _agent.CreatePlanAsync(query, context);

        // Assert
        result.Subtasks[0].Priority.Should().Be(1);
        result.Subtasks[1].Priority.Should().Be(2);
        result.Subtasks[2].Priority.Should().Be(3);
    }

    [Fact]
    public async Task CreatePlanAsync_WithEmptyContext_DoesNotIncludeContextInPrompt()
    {
        // Arrange
        var query = "Test query";
        var emptyContext = new ConversationContext
        {
            ConversationId = "test",
            RecentMessages = Array.Empty<ChatMessage>(),
            RelevantMemories = Array.Empty<MemoryChunk>()
        };

        _mockLLMService.Setup(s => s.GetStructuredOutput<ResearchPlan>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateResearchPlan());

        // Act
        await _agent.CreatePlanAsync(query, emptyContext);

        // Assert
        _mockLLMService.Verify(s => s.GetStructuredOutput<ResearchPlan>(
            It.Is<string>(prompt => !prompt.Contains("Previous Context")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePlanAsync_WithContext_IncludesContextInPrompt()
    {
        // Arrange
        var query = "Test query";
        var context = TestDataBuilder.CreateConversationContext(messageCount: 3);

        _mockLLMService.Setup(s => s.GetStructuredOutput<ResearchPlan>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateResearchPlan());

        // Act
        await _agent.CreatePlanAsync(query, context);

        // Assert
        _mockLLMService.Verify(s => s.GetStructuredOutput<ResearchPlan>(
            It.Is<string>(prompt => prompt.Contains("Previous Context") && prompt.Contains("Recent conversation")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePlanAsync_RespectsCancellationToken()
    {
        // Arrange
        var query = "Test query";
        var context = TestDataBuilder.CreateConversationContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _agent.CreatePlanAsync(query, context, cancellationToken: cts.Token));
    }
}
