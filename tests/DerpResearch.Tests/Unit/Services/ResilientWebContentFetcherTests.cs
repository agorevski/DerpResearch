using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Services;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DerpResearch.Tests.Unit.Services;

public class ResilientWebContentFetcherTests
{
    private readonly Mock<IWebContentFetcher> _mockInnerService;
    private readonly Mock<ILogger<ResilientWebContentFetcher>> _mockLogger;

    public ResilientWebContentFetcherTests()
    {
        _mockInnerService = new Mock<IWebContentFetcher>();
        _mockLogger = TestMockFactory.CreateLogger<ResilientWebContentFetcher>();
    }

    private ResilientWebContentFetcher CreateService(int timeoutSeconds = 5)
    {
        return new ResilientWebContentFetcher(
            _mockInnerService.Object,
            _mockLogger.Object,
            timeoutSeconds);
    }

    [Fact]
    public async Task FetchContentAsync_Success_ReturnsResults()
    {
        // Arrange
        var urls = new[] { "https://example.com" };
        var expected = new Dictionary<string, string> { ["https://example.com"] = "content" };
        
        _mockInnerService
            .Setup(s => s.FetchContentAsync(It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var service = CreateService();

        // Act
        var result = await service.FetchContentAsync(urls);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task FetchContentAsync_InnerServiceFails_ReturnsEmptyDictionary()
    {
        // Arrange
        var urls = new[] { "https://example.com" };
        
        _mockInnerService
            .Setup(s => s.FetchContentAsync(It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        var service = CreateService();

        // Act
        var result = await service.FetchContentAsync(urls);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_SupportsCancellation()
    {
        // Arrange
        var urls = new[] { "https://example.com" };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await service.FetchContentAsync(urls, cancellationToken: cts.Token);
        });
    }

    [Fact]
    public async Task FetchContentAsync_CircuitBreaker_OpensAfterMultipleFailures()
    {
        // Arrange
        var urls = new[] { "https://example.com" };
        
        _mockInnerService
            .Setup(s => s.FetchContentAsync(It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        var service = CreateService();

        // Act - trigger multiple failures to open circuit
        for (int i = 0; i < 6; i++)
        {
            await service.FetchContentAsync(urls);
        }

        // Additional calls should be rejected immediately
        var result = await service.FetchContentAsync(urls);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchContentAsync_UsesConfiguredTimeout()
    {
        // Arrange
        var urls = new[] { "https://example.com" };
        var expected = new Dictionary<string, string> { ["https://example.com"] = "content" };
        int capturedTimeout = 0;
        
        _mockInnerService
            .Setup(s => s.FetchContentAsync(It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string[], int, CancellationToken>((u, t, c) => capturedTimeout = t)
            .ReturnsAsync(expected);

        var service = CreateService(timeoutSeconds: 10);

        // Act
        await service.FetchContentAsync(urls);

        // Assert
        capturedTimeout.Should().Be(10);
    }

    [Fact]
    public async Task FetchContentAsync_SuccessAfterFailure_ResetsCircuitBreaker()
    {
        // Arrange
        var urls = new[] { "https://example.com" };
        var expected = new Dictionary<string, string> { ["https://example.com"] = "content" };
        var callCount = 0;
        
        _mockInnerService
            .Setup(s => s.FetchContentAsync(It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 3)
                    throw new HttpRequestException("Error");
                return expected;
            });

        var service = CreateService();

        // Act - fail twice then succeed
        await service.FetchContentAsync(urls);
        await service.FetchContentAsync(urls);
        var result = await service.FetchContentAsync(urls);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task FetchContentAsync_EmptyUrlArray_ReturnsEmpty()
    {
        // Arrange
        var urls = Array.Empty<string>();
        _mockInnerService
            .Setup(s => s.FetchContentAsync(It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        var service = CreateService();

        // Act
        var result = await service.FetchContentAsync(urls);

        // Assert
        result.Should().BeEmpty();
    }
}
