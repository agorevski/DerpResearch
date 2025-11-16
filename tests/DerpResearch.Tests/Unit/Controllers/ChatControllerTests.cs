using DeepResearch.WebApp.Controllers;
using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using System.Text.Json;

namespace DerpResearch.Tests.Unit.Controllers;

public class ChatControllerTests
{
    private readonly Mock<IOrchestratorService> _mockOrchestrator;
    private readonly Mock<IMemoryService> _mockMemory;
    private readonly Mock<ILogger<ChatController>> _mockLogger;
    private readonly ChatController _controller;

    public ChatControllerTests()
    {
        _mockOrchestrator = TestMockFactory.CreateOrchestratorService();
        _mockMemory = TestMockFactory.CreateMemoryService();
        _mockLogger = TestMockFactory.CreateLogger<ChatController>();
        
        _controller = new ChatController(
            _mockOrchestrator.Object,
            _mockMemory.Object,
            _mockLogger.Object
        );
        
        // Setup HttpContext for SSE tests
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task Chat_ShouldSetCorrectResponseHeaders()
    {
        // Arrange
        var request = TestDataBuilder.CreateChatRequest();
        
        // Act
        await _controller.Chat(request);
        
        // Assert
        _controller.Response.ContentType.Should().Be("text/event-stream");
        _controller.Response.Headers["Cache-Control"].Should().Contain("no-cache");
        _controller.Response.Headers["Connection"].Should().Contain("keep-alive");
    }

    [Fact]
    public async Task Chat_ShouldCreateNewConversation_WhenConversationIdIsNull()
    {
        // Arrange
        var request = TestDataBuilder.CreateChatRequest(conversationId: null);
        
        // Act
        await _controller.Chat(request);
        
        // Assert
        _mockMemory.Verify(m => m.CreateConversationAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Chat_ShouldUseExistingConversation_WhenConversationIdProvided()
    {
        // Arrange
        var existingConvId = "existing-conv-123";
        var request = TestDataBuilder.CreateChatRequest(conversationId: existingConvId);
        
        // Act
        await _controller.Chat(request);
        
        // Assert
        _mockMemory.Verify(m => m.CreateConversationAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockOrchestrator.Verify(
            o => o.ProcessDeepResearchAsync(
                It.IsAny<string>(),
                existingConvId,
                It.IsAny<int>(),
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Chat_ShouldPassCorrectDerpificationLevel()
    {
        // Arrange
        var derpLevel = 50;
        var request = TestDataBuilder.CreateChatRequest(derpificationLevel: derpLevel);
        
        // Act
        await _controller.Chat(request);
        
        // Assert
        _mockOrchestrator.Verify(
            o => o.ProcessDeepResearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                derpLevel,
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Chat_ShouldPassClarificationAnswers_WhenProvided()
    {
        // Arrange
        var answers = new[] { "Answer 1", "Answer 2" };
        var request = TestDataBuilder.CreateChatRequest(clarificationAnswers: answers);
        
        // Act
        await _controller.Chat(request);
        
        // Assert
        _mockOrchestrator.Verify(
            o => o.ProcessDeepResearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                answers,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Chat_ShouldStreamTokensFromOrchestrator()
    {
        // Arrange
        var tokens = new[] { "Token1", "Token2", "Token3" };
        _mockOrchestrator.Setup(o => o.ProcessDeepResearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()))
            .Returns(TestMockFactory.CreateAsyncEnumerable(tokens));
        
        var request = TestDataBuilder.CreateChatRequest();
        
        // Act
        await _controller.Chat(request);
        
        // Assert
        var responseBody = Encoding.UTF8.GetString(
            ((MemoryStream)_controller.Response.Body).ToArray());
        
        responseBody.Should().Contain("Token1");
        responseBody.Should().Contain("Token2");
        responseBody.Should().Contain("Token3");
    }

    [Fact]
    public async Task Chat_ShouldSendCompletionEvent_AtEnd()
    {
        // Arrange
        var request = TestDataBuilder.CreateChatRequest();
        
        // Act
        await _controller.Chat(request);
        
        // Assert
        var responseBody = Encoding.UTF8.GetString(
            ((MemoryStream)_controller.Response.Body).ToArray());
        
        responseBody.Should().Contain("\"type\":\"done\"");
    }

    [Fact]
    public async Task Chat_ShouldHandleError_AndSendErrorEvent()
    {
        // Arrange
        var errorMessage = "Test error";
        _mockOrchestrator.Setup(o => o.ProcessDeepResearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()))
            .Throws(new Exception(errorMessage));
        
        var request = TestDataBuilder.CreateChatRequest();
        
        // Act
        await _controller.Chat(request);
        
        // Assert
        var responseBody = Encoding.UTF8.GetString(
            ((MemoryStream)_controller.Response.Body).ToArray());
        
        responseBody.Should().Contain("\"type\":\"error\"");
        responseBody.Should().Contain(errorMessage);
    }

    [Fact]
    public async Task GetHistory_ShouldReturnConversationHistory()
    {
        // Arrange
        var conversationId = "test-conv-123";
        var context = TestDataBuilder.CreateConversationContext(conversationId);
        
        _mockMemory.Setup(m => m.GetConversationContextAsync(conversationId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);
        
        // Act
        var result = await _controller.GetHistory(conversationId);
        
        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetHistory_ShouldReturn500_OnError()
    {
        // Arrange
        var conversationId = "test-conv-123";
        _mockMemory.Setup(m => m.GetConversationContextAsync(conversationId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));
        
        // Act
        var result = await _controller.GetHistory(conversationId);
        
        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task CreateConversation_ShouldReturnNewConversationId()
    {
        // Arrange
        var newConvId = "new-conv-456";
        _mockMemory.Setup(m => m.CreateConversationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(newConvId);
        
        // Act
        var result = await _controller.CreateConversation();
        
        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        
        var json = JsonSerializer.Serialize(okResult!.Value);
        json.Should().Contain(newConvId);
    }

    [Fact]
    public async Task CreateConversation_ShouldReturn500_OnError()
    {
        // Arrange
        _mockMemory.Setup(m => m.CreateConversationAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));
        
        // Act
        var result = await _controller.CreateConversation();
        
        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }

    [Theory]
    [InlineData(0)]    // Minimum derpification
    [InlineData(50)]   // Medium derpification
    [InlineData(100)]  // Maximum derpification
    public async Task Chat_ShouldHandleDifferentDerpificationLevels(int derpLevel)
    {
        // Arrange
        var request = TestDataBuilder.CreateChatRequest(derpificationLevel: derpLevel);
        
        // Act
        await _controller.Chat(request);
        
        // Assert
        _mockOrchestrator.Verify(
            o => o.ProcessDeepResearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                derpLevel,
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Chat_ShouldHandleJsonStructuredUpdates()
    {
        // Arrange
        var jsonUpdate = "{\"type\":\"progress\",\"data\":{\"message\":\"Searching...\"}}";
        _mockOrchestrator.Setup(o => o.ProcessDeepResearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()))
            .Returns(TestMockFactory.CreateAsyncEnumerable(new[] { jsonUpdate }));
        
        var request = TestDataBuilder.CreateChatRequest();
        
        // Act
        await _controller.Chat(request);
        
        // Assert
        var responseBody = Encoding.UTF8.GetString(
            ((MemoryStream)_controller.Response.Body).ToArray());
        
        responseBody.Should().Contain(jsonUpdate);
        responseBody.Should().Contain("data: {");
    }

    [Fact]
    public async Task Chat_ShouldWrapPlainTextInStreamToken()
    {
        // Arrange
        var plainText = "Plain text response";
        _mockOrchestrator.Setup(o => o.ProcessDeepResearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()))
            .Returns(TestMockFactory.CreateAsyncEnumerable(new[] { plainText }));
        
        var request = TestDataBuilder.CreateChatRequest();
        
        // Act
        await _controller.Chat(request);
        
        // Assert
        var responseBody = Encoding.UTF8.GetString(
            ((MemoryStream)_controller.Response.Body).ToArray());
        
        responseBody.Should().Contain("\"token\"");
        responseBody.Should().Contain(plainText);
        responseBody.Should().Contain("\"conversationId\"");
    }
}
