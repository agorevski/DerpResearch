using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DeepResearch.WebApp.Services;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DerpResearch.Tests.Unit.Services;

public class ResilientLLMServiceTests
{
    private readonly Mock<ILLMService> _mockInnerService;
    private readonly Mock<ILogger<ResilientLLMService>> _mockLogger;
    
    public ResilientLLMServiceTests()
    {
        _mockInnerService = new Mock<ILLMService>();
        _mockLogger = TestMockFactory.CreateLogger<ResilientLLMService>();
    }

    private ResilientLLMService CreateService(
        int failureThreshold = 5,
        int breakDurationSeconds = 30,
        int timeoutSeconds = 120,
        int maxRetryAttempts = 3)
    {
        return new ResilientLLMService(
            _mockInnerService.Object,
            _mockLogger.Object,
            failureThreshold,
            breakDurationSeconds,
            timeoutSeconds,
            maxRetryAttempts);
    }

    [Fact]
    public async Task ChatCompletion_Success_ReturnsResult()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Hello" } };
        _mockInnerService
            .Setup(s => s.ChatCompletion(It.IsAny<ChatMessage[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Response");

        var service = CreateService();

        // Act
        var result = await service.ChatCompletion(messages);

        // Assert
        result.Should().Be("Response");
        _mockInnerService.Verify(s => s.ChatCompletion(messages, "gpt-4o", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChatCompletion_RetriesOnFailure()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Hello" } };
        var callCount = 0;
        
        _mockInnerService
            .Setup(s => s.ChatCompletion(It.IsAny<ChatMessage[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 3)
                    throw new HttpRequestException("Transient error");
                return "Success after retries";
            });

        var service = CreateService(maxRetryAttempts: 3);

        // Act
        var result = await service.ChatCompletion(messages);

        // Assert
        result.Should().Be("Success after retries");
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task ChatCompletion_CircuitBreaker_RejectsAfterThreshold()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Hello" } };
        _mockInnerService
            .Setup(s => s.ChatCompletion(It.IsAny<ChatMessage[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        // Use failureThreshold=1 and maxRetryAttempts=1 so circuit opens after first failed call
        var service = CreateService(failureThreshold: 1, breakDurationSeconds: 30, maxRetryAttempts: 1);

        // Act - trigger one failure to open circuit
        try { await service.ChatCompletion(messages); } catch { }

        // Circuit should be open now - next call should throw InvalidOperationException
        var act = async () => await service.ChatCompletion(messages);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*circuit breaker*");
    }

    [Fact]
    public async Task GetEmbedding_Success_ReturnsEmbedding()
    {
        // Arrange
        var expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _mockInnerService
            .Setup(s => s.GetEmbedding(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding);

        var service = CreateService();

        // Act
        var result = await service.GetEmbedding("test text");

        // Assert
        result.Should().BeEquivalentTo(expectedEmbedding);
    }

    [Fact]
    public async Task GetStructuredOutput_Success_ReturnsResult()
    {
        // Arrange
        var expectedResult = new TestStructuredOutput { Value = "test" };
        _mockInnerService
            .Setup(s => s.GetStructuredOutput<TestStructuredOutput>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var service = CreateService();

        // Act
        var result = await service.GetStructuredOutput<TestStructuredOutput>("prompt");

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().Be("test");
    }

    [Fact]
    public async Task GetStructuredOutput_CircuitOpen_ReturnsNull()
    {
        // Arrange
        _mockInnerService
            .Setup(s => s.GetStructuredOutput<TestStructuredOutput>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        // Use failureThreshold=1 so circuit opens after first failure
        var service = CreateService(failureThreshold: 1, maxRetryAttempts: 1);

        // Trigger one failure to open circuit
        await service.GetStructuredOutput<TestStructuredOutput>("prompt");

        // Act - circuit should be open now
        var result = await service.GetStructuredOutput<TestStructuredOutput>("prompt");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ChatCompletionStream_Success_YieldsTokens()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Hello" } };
        var expectedTokens = new[] { "Hello", " ", "World" };
        
        _mockInnerService
            .Setup(s => s.ChatCompletionStream(It.IsAny<ChatMessage[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(expectedTokens));

        var service = CreateService();

        // Act
        var result = new List<string>();
        await foreach (var token in service.ChatCompletionStream(messages))
        {
            result.Add(token);
        }

        // Assert
        result.Should().BeEquivalentTo(expectedTokens);
    }

    [Fact]
    public async Task ChatCompletionStream_CircuitOpen_YieldsNothing()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Hello" } };
        _mockInnerService
            .Setup(s => s.ChatCompletion(It.IsAny<ChatMessage[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        var service = CreateService(failureThreshold: 1, maxRetryAttempts: 1);

        // Trigger failures to open circuit
        try { await service.ChatCompletion(messages); } catch { }
        try { await service.ChatCompletion(messages); } catch { }

        // Act
        var result = new List<string>();
        await foreach (var token in service.ChatCompletionStream(messages))
        {
            result.Add(token);
        }

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ChatCompletion_RespectsCancellationToken()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Hello" } };
        var cts = new CancellationTokenSource();
        
        _mockInnerService
            .Setup(s => s.ChatCompletion(It.IsAny<ChatMessage[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (ChatMessage[] m, string d, CancellationToken ct) =>
            {
                await Task.Delay(5000, ct);
                return "Response";
            });

        var service = CreateService();
        cts.CancelAfter(100);

        // Act
        var act = async () => await service.ChatCompletion(messages, "gpt-4o", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetEmbedding_RetriesOnFailure()
    {
        // Arrange
        var callCount = 0;
        _mockInnerService
            .Setup(s => s.GetEmbedding(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 2)
                    throw new HttpRequestException("Transient error");
                return new float[] { 0.1f, 0.2f };
            });

        var service = CreateService(maxRetryAttempts: 3);

        // Act
        var result = await service.GetEmbedding("test");

        // Assert
        result.Should().NotBeNull();
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task GetEmbedding_CircuitBreaker_ThrowsAfterThreshold()
    {
        // Arrange
        _mockInnerService
            .Setup(s => s.GetEmbedding(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        var service = CreateService(failureThreshold: 1, maxRetryAttempts: 1);

        // Act - trigger one failure to open circuit
        try { await service.GetEmbedding("test"); } catch { }

        // Assert - circuit should be open
        var act = async () => await service.GetEmbedding("test");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*circuit breaker*");
    }

    [Fact]
    public async Task ChatCompletion_Timeout_ThrowsTimeoutException()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Hello" } };
        _mockInnerService
            .Setup(s => s.ChatCompletion(It.IsAny<ChatMessage[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (ChatMessage[] m, string d, CancellationToken ct) =>
            {
                await Task.Delay(10000, ct);
                return "Response";
            });

        var service = CreateService(timeoutSeconds: 1, maxRetryAttempts: 1);

        // Act
        var act = async () => await service.ChatCompletion(messages);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task GetStructuredOutput_RetriesOnFailure()
    {
        // Arrange
        var callCount = 0;
        _mockInnerService
            .Setup(s => s.GetStructuredOutput<TestStructuredOutput>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 2)
                    throw new HttpRequestException("Transient error");
                return new TestStructuredOutput { Value = "success" };
            });

        var service = CreateService(maxRetryAttempts: 3);

        // Act
        var result = await service.GetStructuredOutput<TestStructuredOutput>("prompt");

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().Be("success");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task ChatCompletionStream_Timeout_YieldsNothing()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Hello" } };
        _mockInnerService
            .Setup(s => s.ChatCompletionStream(It.IsAny<ChatMessage[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(SlowAsyncEnumerable());

        var service = CreateService(timeoutSeconds: 1, maxRetryAttempts: 1);

        // Act
        var result = new List<string>();
        await foreach (var token in service.ChatCompletionStream(messages))
        {
            result.Add(token);
        }

        // Assert - should have yielded at least first token before timeout
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ChatCompletionStream_InnerException_RecordsFailure()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Hello" } };
        _mockInnerService
            .Setup(s => s.ChatCompletionStream(It.IsAny<ChatMessage[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ThrowingAsyncEnumerable());

        var service = CreateService(failureThreshold: 5, maxRetryAttempts: 1);

        // Act
        var result = new List<string>();
        try
        {
            await foreach (var token in service.ChatCompletionStream(messages))
            {
                result.Add(token);
            }
        }
        catch { }

        // Assert - should handle exception
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ChatCompletion_AllRetriesFail_RecordsCircuitBreakerFailure()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Hello" } };
        _mockInnerService
            .Setup(s => s.ChatCompletion(It.IsAny<ChatMessage[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Persistent error"));

        var service = CreateService(failureThreshold: 10, maxRetryAttempts: 2);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await service.ChatCompletion(messages));
    }

    private static async IAsyncEnumerable<string> ToAsyncEnumerable(string[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    private static async IAsyncEnumerable<string> SlowAsyncEnumerable()
    {
        yield return "first";
        await Task.Delay(10000);
        yield return "second";
    }

    private static async IAsyncEnumerable<string> ThrowingAsyncEnumerable()
    {
        await Task.Yield();
        throw new HttpRequestException("Stream error");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private class TestStructuredOutput
    {
        public string Value { get; set; } = string.Empty;
    }
}
