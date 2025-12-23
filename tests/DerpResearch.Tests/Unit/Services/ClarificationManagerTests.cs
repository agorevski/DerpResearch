using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DeepResearch.WebApp.Services;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DerpResearch.Tests.Unit.Services;

public class ClarificationManagerTests
{
    private readonly Mock<IClarificationAgent> _mockClarificationAgent;
    private readonly Mock<IMemoryService> _mockMemoryService;
    private readonly Mock<ILogger<ClarificationManager>> _mockLogger;
    private readonly ClarificationManager _manager;

    public ClarificationManagerTests()
    {
        _mockClarificationAgent = TestMockFactory.CreateClarificationAgent();
        _mockMemoryService = TestMockFactory.CreateMemoryService();
        _mockLogger = TestMockFactory.CreateLogger<ClarificationManager>();

        _manager = new ClarificationManager(
            _mockClarificationAgent.Object,
            _mockMemoryService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GenerateClarificationAsync_WhenAnswersAlreadyProvided_ReturnsNull()
    {
        // Arrange
        var existingAnswers = new[] { "Answer 1", "Answer 2" };

        // Act
        var result = await _manager.GenerateClarificationAsync(
            "test prompt",
            "conv-123",
            "context",
            100,
            existingAnswers);

        // Assert
        result.Should().BeNull();
        _mockClarificationAgent.Verify(a => a.GenerateClarifyingQuestionsAsync(
            It.IsAny<string>(),
            It.IsAny<ConversationContext>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateClarificationAsync_WhenNoAnswers_GeneratesQuestions()
    {
        // Arrange
        var expectedClarification = TestDataBuilder.CreateClarificationResult(3, "Test rationale");
        _mockClarificationAgent
            .Setup(a => a.GenerateClarifyingQuestionsAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationContext>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedClarification);

        // Act
        var result = await _manager.GenerateClarificationAsync(
            "test prompt",
            "conv-123",
            "context",
            100,
            null);

        // Assert
        result.Should().NotBeNull();
        result!.Questions.Should().HaveCount(3);
        result.Rationale.Should().Be("Test rationale");
    }

    [Fact]
    public async Task GenerateClarificationAsync_StoresQuestionsInDatabase()
    {
        // Arrange
        var expectedClarification = TestDataBuilder.CreateClarificationResult();
        _mockClarificationAgent
            .Setup(a => a.GenerateClarifyingQuestionsAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationContext>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedClarification);

        // Act
        await _manager.GenerateClarificationAsync(
            "test prompt",
            "conv-123",
            "context",
            100,
            null);

        // Assert
        _mockMemoryService.Verify(m => m.StoreClarificationQuestionsAsync(
            "conv-123",
            It.IsAny<string[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateClarificationAsync_WithEmptyAnswersArray_GeneratesQuestions()
    {
        // Arrange
        var expectedClarification = TestDataBuilder.CreateClarificationResult();
        _mockClarificationAgent
            .Setup(a => a.GenerateClarifyingQuestionsAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationContext>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedClarification);

        // Act
        var result = await _manager.GenerateClarificationAsync(
            "test prompt",
            "conv-123",
            "context",
            100,
            Array.Empty<string>());

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task EnhancePromptWithClarificationsAsync_BuildsEnhancedPrompt()
    {
        // Arrange
        var questions = new[] { "Q1?", "Q2?" };
        var answers = new[] { "Answer 1", "Answer 2" };

        _mockMemoryService
            .Setup(m => m.GetClarificationQuestionsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(questions);

        // Act
        var result = await _manager.EnhancePromptWithClarificationsAsync(
            "Original prompt",
            "conv-123",
            answers);

        // Assert
        result.Should().Contain("Original prompt");
        result.Should().Contain("Q1?");
        result.Should().Contain("Q2?");
        result.Should().Contain("Answer 1");
        result.Should().Contain("Answer 2");
    }

    [Fact]
    public async Task EnhancePromptWithClarificationsAsync_ClearsQuestionsAfterUse()
    {
        // Arrange
        _mockMemoryService
            .Setup(m => m.GetClarificationQuestionsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Q1?" });

        // Act
        await _manager.EnhancePromptWithClarificationsAsync(
            "Original prompt",
            "conv-123",
            new[] { "Answer 1" });

        // Assert
        _mockMemoryService.Verify(m => m.ClearClarificationQuestionsAsync(
            "conv-123",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnhancePromptWithClarificationsAsync_SkipsEmptyAnswers()
    {
        // Arrange
        var questions = new[] { "Q1?", "Q2?", "Q3?" };
        var answers = new[] { "Answer 1", "", "Answer 3" };

        _mockMemoryService
            .Setup(m => m.GetClarificationQuestionsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(questions);

        // Act
        var result = await _manager.EnhancePromptWithClarificationsAsync(
            "Original prompt",
            "conv-123",
            answers);

        // Assert
        result.Should().Contain("Q1?");
        result.Should().Contain("Answer 1");
        result.Should().NotContain("Q2?");
        result.Should().Contain("Q3?");
        result.Should().Contain("Answer 3");
    }

    [Fact]
    public async Task EnhancePromptWithClarificationsAsync_HandlesMoreQuestionsThanAnswers()
    {
        // Arrange
        var questions = new[] { "Q1?", "Q2?", "Q3?" };
        var answers = new[] { "Answer 1" };

        _mockMemoryService
            .Setup(m => m.GetClarificationQuestionsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(questions);

        // Act
        var result = await _manager.EnhancePromptWithClarificationsAsync(
            "Original prompt",
            "conv-123",
            answers);

        // Assert
        result.Should().Contain("Q1?");
        result.Should().Contain("Answer 1");
        result.Should().NotContain("Q2?");
        result.Should().NotContain("Q3?");
    }

    [Fact]
    public async Task EnhancePromptWithClarificationsAsync_HandlesNullQuestions()
    {
        // Arrange
        _mockMemoryService
            .Setup(m => m.GetClarificationQuestionsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string[]?)null);

        // Act
        var result = await _manager.EnhancePromptWithClarificationsAsync(
            "Original prompt",
            "conv-123",
            new[] { "Answer 1" });

        // Assert
        result.Should().Contain("Original prompt");
    }

    [Fact]
    public async Task GenerateClarificationAsync_RespectsConversationContext()
    {
        // Arrange
        var expectedClarification = TestDataBuilder.CreateClarificationResult();
        _mockClarificationAgent
            .Setup(a => a.GenerateClarifyingQuestionsAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationContext>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedClarification);

        // Act
        await _manager.GenerateClarificationAsync(
            "test prompt",
            "conv-123",
            "context",
            50,
            null);

        // Assert
        _mockClarificationAgent.Verify(a => a.GenerateClarifyingQuestionsAsync(
            "test prompt",
            It.IsAny<ConversationContext>(),
            50,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
