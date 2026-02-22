using DeepResearch.WebApp.Agents;
using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DerpResearch.Tests.Unit.Agents;

public class ReflectionAgentTests
{
    private readonly Mock<ILLMService> _mockLLMService;
    private readonly Mock<ILogger<ReflectionAgent>> _mockLogger;
    private readonly ReflectionAgent _agent;

    public ReflectionAgentTests()
    {
        _mockLLMService = TestMockFactory.CreateLLMService();
        _mockLogger = TestMockFactory.CreateLogger<ReflectionAgent>();
        var aiConfig = Options.Create(new AzureOpenAIConfiguration());
        _agent = new ReflectionAgent(_mockLLMService.Object, _mockLogger.Object, aiConfig);
    }

    [Fact]
    public async Task ReflectAsync_WithValidInput_ReturnsReflectionResult()
    {
        // Arrange
        var userQuery = "What is machine learning?";
        var synthesizedResponse = "Machine learning is a subset of AI [1]. It uses algorithms [2].";
        var info = new GatheredInformation
        {
            Results = TestDataBuilder.CreateSearchResults(5).ToArray(),
            StoredMemoryIds = new[] { "mem1", "mem2" },
            TotalSourcesFound = 5
        };
        var expectedResult = TestDataBuilder.CreateReflectionResult(0.85f);

        _mockLLMService.Setup(s => s.GetStructuredOutput<ReflectionResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _agent.ReflectAsync(userQuery, synthesizedResponse, info);

        // Assert
        result.Should().NotBeNull();
        result.ConfidenceScore.Should().Be(0.85f);
    }

    [Fact]
    public async Task ReflectAsync_WhenLLMReturnsNull_ReturnsFallbackResult()
    {
        // Arrange
        var userQuery = "Test query";
        var synthesizedResponse = "Test response with [1] citation.";
        var info = new GatheredInformation
        {
            Results = TestDataBuilder.CreateSearchResults(3).ToArray(),
            TotalSourcesFound = 3
        };

        _mockLLMService.Setup(s => s.GetStructuredOutput<ReflectionResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReflectionResult?)null);

        // Act
        var result = await _agent.ReflectAsync(userQuery, synthesizedResponse, info);

        // Assert
        result.Should().NotBeNull();
        result.ConfidenceScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReflectAsync_WhenLLMThrows_ReturnsFallbackResult()
    {
        // Arrange
        var userQuery = "Test query";
        var synthesizedResponse = "Test response";
        var info = new GatheredInformation { Results = Array.Empty<SearchResult>(), TotalSourcesFound = 0 };

        _mockLLMService.Setup(s => s.GetStructuredOutput<ReflectionResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM error"));

        // Act
        var result = await _agent.ReflectAsync(userQuery, synthesizedResponse, info);

        // Assert
        result.Should().NotBeNull();
        result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0);
    }

    [Theory]
    [InlineData(0)]   // Derp mode
    [InlineData(33)]  // Edge of derp mode
    [InlineData(50)]  // Average mode
    [InlineData(66)]  // Edge of average mode
    [InlineData(100)] // Smart mode
    public async Task ReflectAsync_WithDifferentDerpificationLevels_AdjustsEvaluation(int derpLevel)
    {
        // Arrange
        var userQuery = "Test query";
        var synthesizedResponse = "Test response";
        var info = new GatheredInformation { Results = Array.Empty<SearchResult>(), TotalSourcesFound = 0 };

        _mockLLMService.Setup(s => s.GetStructuredOutput<ReflectionResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateReflectionResult());

        // Act
        await _agent.ReflectAsync(userQuery, synthesizedResponse, info, derpLevel);

        // Assert
        _mockLLMService.Verify(s => s.GetStructuredOutput<ReflectionResult>(
            It.Is<string>(prompt => derpLevel <= 33 
                ? prompt.Contains("SIMPLE") 
                : derpLevel <= 66 
                    ? prompt.Contains("BALANCED") 
                    : prompt.Contains("RIGOROUS")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReflectAsync_WithMultipleCitations_ReturnsHigherConfidence()
    {
        // Arrange
        var userQuery = "Test query";
        var synthesizedResponse = "Response with [1], [2], [3], [4], [5] citations.";
        var info = new GatheredInformation
        {
            Results = TestDataBuilder.CreateSearchResults(5).ToArray(),
            TotalSourcesFound = 5
        };

        _mockLLMService.Setup(s => s.GetStructuredOutput<ReflectionResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReflectionResult?)null);

        // Act
        var result = await _agent.ReflectAsync(userQuery, synthesizedResponse, info);

        // Assert
        result.ConfidenceScore.Should().BeGreaterThan(0.5f);
    }

    [Fact]
    public async Task ReflectAsync_LowConfidence_SetsRequiresMoreResearch()
    {
        // Arrange
        var userQuery = "Test query";
        var synthesizedResponse = "Short response.";
        var info = new GatheredInformation { Results = Array.Empty<SearchResult>(), TotalSourcesFound = 0 };

        _mockLLMService.Setup(s => s.GetStructuredOutput<ReflectionResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReflectionResult?)null);

        // Act
        var result = await _agent.ReflectAsync(userQuery, synthesizedResponse, info);

        // Assert
        if (result.ConfidenceScore < 0.7f)
        {
            result.RequiresMoreResearch.Should().BeTrue();
            result.IdentifiedGaps.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task ReflectAsync_RespectsCancellationToken()
    {
        // Arrange
        var userQuery = "Test query";
        var synthesizedResponse = "Test response";
        var info = new GatheredInformation { Results = Array.Empty<SearchResult>(), TotalSourcesFound = 0 };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _agent.ReflectAsync(userQuery, synthesizedResponse, info, cancellationToken: cts.Token));
    }
}
