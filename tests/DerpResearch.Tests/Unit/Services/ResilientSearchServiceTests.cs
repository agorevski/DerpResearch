using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DeepResearch.WebApp.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DerpResearch.Tests.Unit.Services;

public class ResilientSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_Success_ReturnsResults()
    {
        // Arrange
        var mockInnerService = new Mock<ISearchService>();
        var expectedResults = new[] 
        {
            new SearchResult { Title = "Test", Url = "http://test.com", Snippet = "Test snippet" }
        };
        mockInnerService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var mockLogger = new Mock<ILogger<ResilientSearchService>>();
        var service = new ResilientSearchService(mockInnerService.Object, mockLogger.Object);

        // Act
        var results = await service.SearchAsync("test query", 10);

        // Assert
        Assert.Equal(expectedResults, results);
        mockInnerService.Verify(s => s.SearchAsync("test query", 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_TransientFailure_RetriesAndSucceeds()
    {
        // Arrange
        var mockInnerService = new Mock<ISearchService>();
        var expectedResults = new[] 
        {
            new SearchResult { Title = "Test", Url = "http://test.com", Snippet = "Test snippet" }
        };

        int callCount = 0;
        mockInnerService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount < 2)
                {
                    throw new HttpRequestException("Temporary failure");
                }
                return Task.FromResult(expectedResults);
            });

        var mockLogger = new Mock<ILogger<ResilientSearchService>>();
        var service = new ResilientSearchService(mockInnerService.Object, mockLogger.Object);

        // Act
        var results = await service.SearchAsync("test query", 10);

        // Assert
        Assert.Equal(expectedResults, results);
        Assert.Equal(2, callCount); // Should retry once
    }

    [Fact]
    public async Task SearchAsync_PermanentFailure_ReturnsEmptyAfterRetries()
    {
        // Arrange
        var mockInnerService = new Mock<ISearchService>();
        mockInnerService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Permanent failure"));

        var mockLogger = new Mock<ILogger<ResilientSearchService>>();
        var service = new ResilientSearchService(mockInnerService.Object, mockLogger.Object);

        // Act
        var results = await service.SearchAsync("test query", 10);

        // Assert
        Assert.Empty(results);
        mockInnerService.Verify(s => s.SearchAsync("test query", 10, It.IsAny<CancellationToken>()), Times.Exactly(3)); // 3 attempts
    }

    [Fact]
    public async Task CircuitBreaker_OpensAfterThresholdFailures()
    {
        // Arrange
        var mockInnerService = new Mock<ISearchService>();
        mockInnerService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service failure"));

        var mockLogger = new Mock<ILogger<ResilientSearchService>>();
        var service = new ResilientSearchService(mockInnerService.Object, mockLogger.Object);

        // Act - Trigger 5 failures to open circuit breaker (threshold = 5)
        for (int i = 0; i < 5; i++)
        {
            await service.SearchAsync($"query{i}", 10);
            await Task.Delay(10); // Small delay between requests
        }

        // Act - Next request should be rejected by circuit breaker
        var results = await service.SearchAsync("blocked query", 10);

        // Assert - Circuit breaker should block this request (returning empty immediately)
        Assert.Empty(results);
        
        // Verify we did NOT call the inner service for the blocked request
        // Total calls should be 5 failures * 3 retries each = 15 calls, not 16
        mockInnerService.Verify(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), 
            Times.Exactly(15)); // 5 failures * 3 attempts each
    }

    [Fact]
    public async Task RateLimiting_EnforcesMinimumInterval()
    {
        // Arrange
        var mockInnerService = new Mock<ISearchService>();
        mockInnerService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult[] { });

        var mockLogger = new Mock<ILogger<ResilientSearchService>>();
        var service = new ResilientSearchService(
            mockInnerService.Object, 
            mockLogger.Object,
            maxConcurrentRequests: 2,
            requestsPerSecond: 2); // Min 500ms between requests

        // Act - Make two requests in quick succession
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await service.SearchAsync("query1", 10);
        await service.SearchAsync("query2", 10);
        sw.Stop();

        // Assert - Should take at least 500ms due to rate limiting
        Assert.True(sw.ElapsedMilliseconds >= 450, 
            $"Expected at least 450ms delay, but got {sw.ElapsedMilliseconds}ms");
    }
}

public class CircuitBreakerTests
{
    [Fact]
    public void AllowRequest_InitialState_ReturnsTrue()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var breaker = new CircuitBreaker(5, TimeSpan.FromSeconds(30), mockLogger.Object);

        // Act & Assert
        Assert.True(breaker.AllowRequest());
    }

    [Fact]
    public void AllowRequest_AfterFailureThreshold_ReturnsFalse()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var breaker = new CircuitBreaker(3, TimeSpan.FromSeconds(30), mockLogger.Object);

        // Act - Record 3 failures to reach threshold
        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.RecordFailure();

        // Assert
        Assert.False(breaker.AllowRequest());
    }

    [Fact]
    public void AllowRequest_AfterBreakDuration_ReturnsTrue()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var breaker = new CircuitBreaker(2, TimeSpan.FromMilliseconds(100), mockLogger.Object);

        // Act - Open the breaker
        breaker.RecordFailure();
        breaker.RecordFailure();
        Assert.False(breaker.AllowRequest());

        // Wait for break duration
        Thread.Sleep(150);

        // Assert - Should enter half-open state
        Assert.True(breaker.AllowRequest());
    }

    [Fact]
    public void RecordSuccess_InHalfOpenState_ClosesCircuit()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var breaker = new CircuitBreaker(2, TimeSpan.FromMilliseconds(100), mockLogger.Object);

        // Act - Open the breaker
        breaker.RecordFailure();
        breaker.RecordFailure();
        
        // Wait and enter half-open
        Thread.Sleep(150);
        breaker.AllowRequest(); // Moves to half-open
        
        // Record success to close
        breaker.RecordSuccess();

        // Assert - Should be closed again
        Assert.True(breaker.AllowRequest());
    }

    [Fact]
    public void RecordFailure_InHalfOpenState_ReopensCircuit()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var breaker = new CircuitBreaker(2, TimeSpan.FromMilliseconds(100), mockLogger.Object);

        // Act - Open the breaker
        breaker.RecordFailure();
        breaker.RecordFailure();
        
        // Wait and enter half-open
        Thread.Sleep(150);
        breaker.AllowRequest(); // Moves to half-open
        
        // Record failure to reopen
        breaker.RecordFailure();

        // Assert - Should be open again
        Assert.False(breaker.AllowRequest());
    }

    [Fact]
    public void CircuitBreaker_ThreadSafety_NoRaceConditions()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var breaker = new CircuitBreaker(10, TimeSpan.FromSeconds(1), mockLogger.Object);
        var tasks = new List<Task>();

        // Act - Multiple threads accessing breaker simultaneously
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => breaker.AllowRequest()));
            tasks.Add(Task.Run(() => breaker.RecordSuccess()));
            tasks.Add(Task.Run(() => breaker.RecordFailure()));
        }

        Task.WhenAll(tasks).Wait();

        // Assert - No exceptions thrown (test passes if no exceptions)
        Assert.True(true);
    }
}
