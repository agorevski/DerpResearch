using DeepResearch.WebApp.Services;
using FluentAssertions;
using Xunit;

namespace DerpResearch.Tests.Unit.Services;

public class ProgressStreamingServiceTests
{
    private readonly ProgressStreamingService _service;

    public ProgressStreamingServiceTests()
    {
        _service = new ProgressStreamingService();
    }

    [Fact]
    public void CreateProgressToken_ReturnsValidJson()
    {
        // Act
        var result = _service.CreateProgressToken("conv-123", "searching", "Finding sources...");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("conv-123");
        result.Should().Contain("searching");
        result.Should().Contain("Finding sources...");
        result.Should().Contain("progress");
        result.Should().EndWith("\n");
    }

    [Fact]
    public void CreateProgressToken_WithDetails_IncludesDetails()
    {
        // Act
        var result = _service.CreateProgressToken("conv-123", "processing", "Working...", new { count = 5 });

        // Assert
        result.Should().Contain("count");
        result.Should().Contain("5");
    }

    [Fact]
    public void CreatePlanToken_ReturnsValidJson()
    {
        // Arrange
        var subtasks = new[] { "Task 1", "Task 2", "Task 3" };

        // Act
        var result = _service.CreatePlanToken("conv-123", "Research topic", subtasks);

        // Assert
        result.Should().Contain("conv-123");
        result.Should().Contain("Research topic");
        result.Should().Contain("Task 1");
        result.Should().Contain("Task 2");
        result.Should().Contain("Task 3");
        result.Should().Contain("plan");
    }

    [Fact]
    public void CreateSearchQueryToken_ReturnsValidJson()
    {
        // Act
        var result = _service.CreateSearchQueryToken("conv-123", "test query", 2, 5);

        // Assert
        result.Should().Contain("conv-123");
        result.Should().Contain("test query");
        result.Should().Contain("2");
        result.Should().Contain("5");
        result.Should().Contain("search_query");
    }

    [Fact]
    public void CreateSourceToken_ReturnsValidJson()
    {
        // Act
        var result = _service.CreateSourceToken("conv-123", "Test Article", "https://example.com", "Test snippet");

        // Assert
        result.Should().Contain("conv-123");
        result.Should().Contain("Test Article");
        result.Should().Contain("https://example.com");
        result.Should().Contain("Test snippet");
        result.Should().Contain("source");
    }

    [Fact]
    public void CreateSourceToken_WithNullSnippet_StillReturnsValidJson()
    {
        // Act
        var result = _service.CreateSourceToken("conv-123", "Test Article", "https://example.com", null);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Test Article");
    }

    [Fact]
    public void CreateClarificationToken_ReturnsValidJson()
    {
        // Arrange
        var questions = new[] { "Question 1?", "Question 2?" };

        // Act
        var result = _service.CreateClarificationToken("conv-123", questions, "Need more info");

        // Assert
        result.Should().Contain("conv-123");
        result.Should().Contain("Question 1?");
        result.Should().Contain("Question 2?");
        result.Should().Contain("Need more info");
        result.Should().Contain("clarification");
    }

    [Fact]
    public void CreateReflectionToken_ReturnsValidJson()
    {
        // Act
        var result = _service.CreateReflectionToken("conv-123", 0.85, "Good quality response", 2);

        // Assert
        result.Should().Contain("conv-123");
        result.Should().Contain("0.85");
        result.Should().Contain("Good quality response");
        result.Should().Contain("2");
        result.Should().Contain("reflection");
    }

    [Fact]
    public void AllTokens_EndWithNewline()
    {
        // Act & Assert
        _service.CreateProgressToken("c", "s", "m").Should().EndWith("\n");
        _service.CreatePlanToken("c", "g", new[] { "t" }).Should().EndWith("\n");
        _service.CreateSearchQueryToken("c", "q", 1, 1).Should().EndWith("\n");
        _service.CreateSourceToken("c", "t", "u", "s").Should().EndWith("\n");
        _service.CreateClarificationToken("c", new[] { "q" }, "r").Should().EndWith("\n");
        _service.CreateReflectionToken("c", 0.5, "r", 1).Should().EndWith("\n");
    }

    [Fact]
    public void CreateProgressToken_UsesCamelCaseJsonNaming()
    {
        // Act
        var result = _service.CreateProgressToken("conv-123", "test", "message");

        // Assert
        result.Should().Contain("conversationId");
        result.Should().NotContain("ConversationId");
    }
}
