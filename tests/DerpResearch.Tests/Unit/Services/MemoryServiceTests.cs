using DeepResearch.WebApp.Models;
using DerpResearch.Tests.Helpers;
using FluentAssertions;

namespace DerpResearch.Tests.Unit.Services;

/// <summary>
/// Tests for StoreMemoryResult and related functionality
/// </summary>
public class StoreMemoryResultTests
{
    [Fact]
    public void IsFullySuccessful_WhenAllChunksSucceed_ReturnsTrue()
    {
        // Arrange
        var result = new StoreMemoryResult
        {
            PrimaryId = "test-id",
            TotalChunks = 3,
            SuccessfulChunks = 3,
            FailedChunks = 0
        };

        // Act & Assert
        result.IsFullySuccessful.Should().BeTrue();
        result.IsPartiallySuccessful.Should().BeFalse();
        result.IsCompleteFailure.Should().BeFalse();
    }

    [Fact]
    public void IsPartiallySuccessful_WhenSomeChunksFail_ReturnsTrue()
    {
        // Arrange
        var result = new StoreMemoryResult
        {
            PrimaryId = "test-id",
            TotalChunks = 3,
            SuccessfulChunks = 2,
            FailedChunks = 1,
            Errors = new List<ChunkError>
            {
                new ChunkError
                {
                    ChunkIndex = 2,
                    ChunkId = "test-id-chunk2",
                    ErrorMessage = "Failed to generate embedding",
                    ExceptionType = "HttpRequestException"
                }
            }
        };

        // Act & Assert
        result.IsFullySuccessful.Should().BeFalse();
        result.IsPartiallySuccessful.Should().BeTrue();
        result.IsCompleteFailure.Should().BeFalse();
    }

    [Fact]
    public void IsCompleteFailure_WhenAllChunksFail_ReturnsTrue()
    {
        // Arrange
        var result = new StoreMemoryResult
        {
            PrimaryId = "test-id",
            TotalChunks = 2,
            SuccessfulChunks = 0,
            FailedChunks = 2,
            Errors = new List<ChunkError>
            {
                new ChunkError { ChunkIndex = 0, ChunkId = "test-id-chunk0", ErrorMessage = "Error 1" },
                new ChunkError { ChunkIndex = 1, ChunkId = "test-id-chunk1", ErrorMessage = "Error 2" }
            }
        };

        // Act & Assert
        result.IsFullySuccessful.Should().BeFalse();
        result.IsPartiallySuccessful.Should().BeFalse();
        result.IsCompleteFailure.Should().BeTrue();
    }

    [Fact]
    public void Empty_ReturnsEmptyResult()
    {
        // Act
        var result = StoreMemoryResult.Empty();

        // Assert
        result.TotalChunks.Should().Be(0);
        result.SuccessfulChunks.Should().Be(0);
        result.FailedChunks.Should().Be(0);
        result.Errors.Should().BeEmpty();
        result.IsFullySuccessful.Should().BeFalse();
    }

    [Fact]
    public void TestDataBuilder_CreateStoreMemoryResult_ReturnsValidResult()
    {
        // Act
        var result = TestDataBuilder.CreateStoreMemoryResult();

        // Assert
        result.PrimaryId.Should().NotBeEmpty();
        result.TotalChunks.Should().Be(1);
        result.SuccessfulChunks.Should().Be(1);
        result.IsFullySuccessful.Should().BeTrue();
    }

    [Fact]
    public void TestDataBuilder_CreateFailedStoreMemoryResult_ReturnsFailedResult()
    {
        // Act
        var result = TestDataBuilder.CreateFailedStoreMemoryResult(
            primaryId: "failed-id",
            totalChunks: 3,
            errorMessage: "Embedding service unavailable");

        // Assert
        result.PrimaryId.Should().Be("failed-id");
        result.TotalChunks.Should().Be(3);
        result.SuccessfulChunks.Should().Be(0);
        result.FailedChunks.Should().Be(3);
        result.IsCompleteFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(3);
        result.Errors.All(e => e.ErrorMessage == "Embedding service unavailable").Should().BeTrue();
    }

    [Fact]
    public void ChunkError_ContainsAllRelevantInformation()
    {
        // Arrange
        var error = new ChunkError
        {
            ChunkIndex = 5,
            ChunkId = "memory-123-chunk5",
            ErrorMessage = "Connection timeout",
            ExceptionType = "TimeoutException"
        };

        // Assert
        error.ChunkIndex.Should().Be(5);
        error.ChunkId.Should().Be("memory-123-chunk5");
        error.ErrorMessage.Should().Be("Connection timeout");
        error.ExceptionType.Should().Be("TimeoutException");
    }
}
