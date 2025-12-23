using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DeepResearch.WebApp.Services;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DerpResearch.Tests.Unit.Services;

public class MockLLMServiceTests
{
    private readonly Mock<ILogger<MockLLMService>> _mockLogger;
    private readonly MockLLMService _service;

    public MockLLMServiceTests()
    {
        _mockLogger = TestMockFactory.CreateLogger<MockLLMService>();
        _service = new MockLLMService(_mockLogger.Object);
    }

    [Fact]
    public async Task ChatCompletionStream_YieldsTokens()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Hello" } };

        // Act
        var tokens = new List<string>();
        await foreach (var token in _service.ChatCompletionStream(messages))
        {
            tokens.Add(token);
        }

        // Assert
        tokens.Should().NotBeEmpty();
        tokens.Should().AllSatisfy(t => t.Should().EndWith(" "));
    }

    [Fact]
    public async Task ChatCompletionStream_RespectsDeploymentName()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Test" } };

        // Act
        var tokens = new List<string>();
        await foreach (var token in _service.ChatCompletionStream(messages, "custom-model"))
        {
            tokens.Add(token);
        }

        // Assert
        tokens.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ChatCompletion_ReturnsResponse()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Hello" } };

        // Act
        var result = await _service.ChatCompletion(messages);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ChatCompletion_ReturnsContextualResponse_ForCompareQuery()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Compare these two options" } };

        // Act
        var result = await _service.ChatCompletion(messages);

        // Assert
        result.Should().Contain("comparison");
    }

    [Fact]
    public async Task ChatCompletion_ReturnsContextualResponse_ForHowQuery()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "How does this work?" } };

        // Act
        var result = await _service.ChatCompletion(messages);

        // Assert
        result.Should().Contain("explain");
    }

    [Fact]
    public async Task ChatCompletion_ReturnsContextualResponse_ForWhatQuery()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "What is machine learning?" } };

        // Act
        var result = await _service.ChatCompletion(messages);

        // Assert
        result.Should().Contain("concept");
    }

    [Fact]
    public async Task GetEmbedding_ReturnsDeterministicEmbedding()
    {
        // Arrange
        var text = "test text";

        // Act
        var embedding1 = await _service.GetEmbedding(text);
        var embedding2 = await _service.GetEmbedding(text);

        // Assert
        embedding1.Should().HaveCount(3072);
        embedding1.Should().BeEquivalentTo(embedding2);
    }

    [Fact]
    public async Task GetEmbedding_ReturnsNormalizedEmbedding()
    {
        // Arrange
        var text = "test text";

        // Act
        var embedding = await _service.GetEmbedding(text);

        // Assert
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        magnitude.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public async Task GetStructuredOutput_ReturnsResearchPlan()
    {
        // Act
        var result = await _service.GetStructuredOutput<ResearchPlan>("Test query");

        // Assert
        result.Should().NotBeNull();
        result!.MainGoal.Should().Contain("Test query");
        result.Subtasks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetStructuredOutput_ReturnsReflectionResult()
    {
        // Act
        var result = await _service.GetStructuredOutput<ReflectionResult>("Test query");

        // Assert
        result.Should().NotBeNull();
        result!.ConfidenceScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetStructuredOutput_ReturnsClarificationResult()
    {
        // Act
        var result = await _service.GetStructuredOutput<ClarificationResult>("Test query");

        // Assert
        result.Should().NotBeNull();
        result!.Questions.Should().NotBeEmpty();
        result.Rationale.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetStructuredOutput_ReturnsNullForUnknownType()
    {
        // Act
        var result = await _service.GetStructuredOutput<UnknownTestType>("Test query");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ChatCompletionStream_SupportsCancellation()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Test" } };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var token in _service.ChatCompletionStream(messages, "gpt-4o", cts.Token))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task ChatCompletion_SupportsCancellation()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Test" } };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _service.ChatCompletion(messages, "gpt-4o", cts.Token);
        });
    }

    [Fact]
    public async Task GetEmbedding_SupportsCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _service.GetEmbedding("test", cts.Token);
        });
    }

    [Fact]
    public async Task GetStructuredOutput_SupportsCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _service.GetStructuredOutput<ResearchPlan>("test", "gpt-4o", cts.Token);
        });
    }

    [Fact]
    public async Task ChatCompletion_HandlesEmptyMessages()
    {
        // Arrange
        var messages = Array.Empty<ChatMessage>();

        // Act
        var result = await _service.ChatCompletion(messages);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    private class UnknownTestType
    {
        public string Value { get; set; } = string.Empty;
    }
}
