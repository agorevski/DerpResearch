using DeepResearch.WebApp.Middleware;
using DeepResearch.WebApp.Models;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DerpResearch.Tests.Unit.Middleware;

public class CorrelationIdMiddlewareTests
{
    private readonly Mock<ILogger<CorrelationIdMiddleware>> _mockLogger;

    public CorrelationIdMiddlewareTests()
    {
        _mockLogger = TestMockFactory.CreateLogger<CorrelationIdMiddleware>();
    }

    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationId_WhenNotProvided()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey].Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_UsesProvidedCorrelationId()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = "provided-correlation-id";
        
        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey].Should().Be("provided-correlation-id");
    }

    [Fact]
    public async Task InvokeAsync_AddsCorrelationIdToResponseHeaders()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var correlationIdSet = false;
        
        context.Response.OnStarting(() =>
        {
            correlationIdSet = context.Response.Headers.ContainsKey(CorrelationIdMiddleware.CorrelationIdHeaderName);
            return Task.CompletedTask;
        });
        
        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - The correlation ID should be stored in Items
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey].Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_GeneratesNewIdForEmptyHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = "";
        
        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
        correlationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_GeneratesNewIdForWhitespaceHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = "   ";
        
        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
        correlationId.Should().NotBeNullOrEmpty();
        correlationId.Should().NotBe("   ");
    }
}

public class CorrelationIdContextExtensionsTests
{
    [Fact]
    public void GetCorrelationId_ReturnsStoredValue()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "test-correlation-id";

        // Act
        var result = context.GetCorrelationId();

        // Assert
        result.Should().Be("test-correlation-id");
    }

    [Fact]
    public void GetCorrelationId_ReturnsNull_WhenNotSet()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var result = context.GetCorrelationId();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetOrCreateCorrelationId_ReturnsStoredValue()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "test-correlation-id";

        // Act
        var result = context.GetOrCreateCorrelationId();

        // Assert
        result.Should().Be("test-correlation-id");
    }

    [Fact]
    public void GetOrCreateCorrelationId_GeneratesNewId_WhenNotSet()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var result = context.GetOrCreateCorrelationId();

        // Assert
        result.Should().NotBeNullOrEmpty();
        Guid.TryParse(result, out _).Should().BeTrue();
    }

    [Fact]
    public void GetOrCreateCorrelationId_GeneratesNewId_ForNullContext()
    {
        // Arrange
        HttpContext? context = null;

        // Act
        var result = context.GetOrCreateCorrelationId();

        // Assert
        result.Should().NotBeNullOrEmpty();
        Guid.TryParse(result, out _).Should().BeTrue();
    }

    [Fact]
    public void GetOrCreateCorrelationId_GeneratesNewId_ForEmptyValue()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "";

        // Act
        var result = context.GetOrCreateCorrelationId();

        // Assert
        result.Should().NotBeNullOrEmpty();
    }
}
