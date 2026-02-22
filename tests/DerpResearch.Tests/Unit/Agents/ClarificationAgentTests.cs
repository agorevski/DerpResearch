using DeepResearch.WebApp.Agents;
using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DerpResearch.Tests.Unit.Agents;

public class ClarificationAgentTests
{
    private readonly Mock<ILLMService> _mockLLMService;
    private readonly Mock<ILogger<ClarificationAgent>> _mockLogger;
    private readonly ClarificationAgent _agent;

    public ClarificationAgentTests()
    {
        _mockLLMService = TestMockFactory.CreateLLMService();
        _mockLogger = TestMockFactory.CreateLogger<ClarificationAgent>();
        var aiConfig = Options.Create(new AzureOpenAIConfiguration());
        _agent = new ClarificationAgent(_mockLLMService.Object, _mockLogger.Object, aiConfig);
    }

    [Fact]
    public async Task GenerateClarifyingQuestionsAsync_WithValidQuery_ReturnsQuestions()
    {
        // Arrange
        var query = "What is the best programming language?";
        var context = TestDataBuilder.CreateConversationContext();
        var expectedResult = TestDataBuilder.CreateClarificationResult(3);

        _mockLLMService.Setup(s => s.GetStructuredOutput<ClarificationResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _agent.GenerateClarifyingQuestionsAsync(query, context);

        // Assert
        result.Should().NotBeNull();
        result.Questions.Should().NotBeEmpty();
        result.Rationale.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateClarifyingQuestionsAsync_WhenLLMReturnsNull_ReturnsFallback()
    {
        // Arrange
        var query = "Test query";
        var context = TestDataBuilder.CreateConversationContext();

        _mockLLMService.Setup(s => s.GetStructuredOutput<ClarificationResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClarificationResult?)null);

        // Act
        var result = await _agent.GenerateClarifyingQuestionsAsync(query, context);

        // Assert
        result.Should().NotBeNull();
        result.Questions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateClarifyingQuestionsAsync_WhenLLMReturnsEmptyQuestions_ReturnsFallback()
    {
        // Arrange
        var query = "Test query";
        var context = TestDataBuilder.CreateConversationContext();

        _mockLLMService.Setup(s => s.GetStructuredOutput<ClarificationResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClarificationResult { Questions = Array.Empty<string>(), Rationale = "" });

        // Act
        var result = await _agent.GenerateClarifyingQuestionsAsync(query, context);

        // Assert
        result.Should().NotBeNull();
        result.Questions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateClarifyingQuestionsAsync_WhenLLMThrows_ReturnsFallback()
    {
        // Arrange
        var query = "Test query";
        var context = TestDataBuilder.CreateConversationContext();

        _mockLLMService.Setup(s => s.GetStructuredOutput<ClarificationResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM error"));

        // Act
        var result = await _agent.GenerateClarifyingQuestionsAsync(query, context);

        // Assert
        result.Should().NotBeNull();
        result.Questions.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(0, 2)]     // Derp mode: fewer questions
    [InlineData(33, 2)]    // Edge of derp mode
    [InlineData(50, 3)]    // Average mode
    [InlineData(66, 3)]    // Edge of average mode
    [InlineData(100, 4)]   // Smart mode: more questions
    public async Task GenerateClarifyingQuestionsAsync_DerpificationLevel_AffectsQuestionCount(int derpLevel, int expectedMinQuestions)
    {
        // Arrange
        var query = "Test query";
        var context = TestDataBuilder.CreateConversationContext();

        _mockLLMService.Setup(s => s.GetStructuredOutput<ClarificationResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClarificationResult?)null);

        // Act
        var result = await _agent.GenerateClarifyingQuestionsAsync(query, context, derpLevel);

        // Assert
        result.Questions.Length.Should().BeGreaterThanOrEqualTo(expectedMinQuestions);
    }

    [Theory]
    [InlineData(0)]   // Derp mode
    [InlineData(50)]  // Average mode
    [InlineData(100)] // Smart mode
    public async Task GenerateClarifyingQuestionsAsync_DerpificationLevel_AffectsPromptStyle(int derpLevel)
    {
        // Arrange
        var query = "Test query";
        var context = TestDataBuilder.CreateConversationContext();

        _mockLLMService.Setup(s => s.GetStructuredOutput<ClarificationResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateClarificationResult());

        // Act
        await _agent.GenerateClarifyingQuestionsAsync(query, context, derpLevel);

        // Assert
        _mockLLMService.Verify(s => s.GetStructuredOutput<ClarificationResult>(
            It.Is<string>(prompt => derpLevel <= 33 
                ? prompt.Contains("SUPER SIMPLE") 
                : derpLevel <= 66 
                    ? prompt.Contains("FOCUSED") 
                    : prompt.Contains("NUANCED")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateClarifyingQuestionsAsync_WithEmptyContext_DoesNotIncludeContextInPrompt()
    {
        // Arrange
        var query = "Test query";
        var emptyContext = new ConversationContext
        {
            ConversationId = "test",
            RecentMessages = Array.Empty<ChatMessage>(),
            RelevantMemories = Array.Empty<MemoryChunk>()
        };

        _mockLLMService.Setup(s => s.GetStructuredOutput<ClarificationResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateClarificationResult());

        // Act
        await _agent.GenerateClarifyingQuestionsAsync(query, emptyContext);

        // Assert
        _mockLLMService.Verify(s => s.GetStructuredOutput<ClarificationResult>(
            It.Is<string>(prompt => !prompt.Contains("Previous Context")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateClarifyingQuestionsAsync_RespectsCancellationToken()
    {
        // Arrange
        var query = "Test query";
        var context = TestDataBuilder.CreateConversationContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _agent.GenerateClarifyingQuestionsAsync(query, context, cancellationToken: cts.Token));
    }
}
