using DeepResearch.WebApp.Models;
using FluentAssertions;
using Xunit;

namespace DerpResearch.Tests.Unit.Models;

public class DTOsTests
{
    [Fact]
    public void ChatRequest_DefaultValues_AreCorrect()
    {
        // Act
        var request = new ChatRequest("Test prompt");

        // Assert
        request.Prompt.Should().Be("Test prompt");
        request.Mode.Should().Be("deep-research");
        request.ConversationId.Should().BeNull();
        request.DerpificationLevel.Should().Be(100);
        request.ClarificationAnswers.Should().BeNull();
    }

    [Fact]
    public void ChatRequest_WithAllParameters_SetsValues()
    {
        // Arrange
        var answers = new[] { "Answer 1", "Answer 2" };

        // Act
        var request = new ChatRequest(
            "Test prompt",
            "custom-mode",
            "conv-123",
            50,
            answers);

        // Assert
        request.Prompt.Should().Be("Test prompt");
        request.Mode.Should().Be("custom-mode");
        request.ConversationId.Should().Be("conv-123");
        request.DerpificationLevel.Should().Be(50);
        request.ClarificationAnswers.Should().BeEquivalentTo(answers);
    }

    [Fact]
    public void ChatResponse_SetsValues()
    {
        // Act
        var response = new ChatResponse("conv-123", "Hello world");

        // Assert
        response.ConversationId.Should().Be("conv-123");
        response.Message.Should().Be("Hello world");
    }

    [Fact]
    public void StreamToken_DefaultType_IsContent()
    {
        // Act
        var token = new StreamToken("test token");

        // Assert
        token.Token.Should().Be("test token");
        token.Type.Should().Be(StreamTokenTypes.Content);
    }

    [Fact]
    public void StreamToken_WithAllParameters_SetsValues()
    {
        // Act
        var token = new StreamToken(
            "token",
            "conv-123",
            StreamTokenTypes.Progress,
            new { stage = "test" });

        // Assert
        token.Token.Should().Be("token");
        token.ConversationId.Should().Be("conv-123");
        token.Type.Should().Be(StreamTokenTypes.Progress);
        token.Data.Should().NotBeNull();
    }

    [Fact]
    public void ProgressUpdate_SetsValues()
    {
        // Act
        var update = new ProgressUpdate("searching", "Finding sources...", new { count = 5 });

        // Assert
        update.Stage.Should().Be("searching");
        update.Message.Should().Be("Finding sources...");
        update.Details.Should().NotBeNull();
    }

    [Fact]
    public void SearchQueryUpdate_SetsValues()
    {
        // Act
        var update = new SearchQueryUpdate("test query", 2, 5);

        // Assert
        update.Query.Should().Be("test query");
        update.TaskNumber.Should().Be(2);
        update.TotalTasks.Should().Be(5);
    }

    [Fact]
    public void SourceUpdate_SetsValues()
    {
        // Act
        var update = new SourceUpdate("Title", "https://example.com", "Snippet");

        // Assert
        update.Title.Should().Be("Title");
        update.Url.Should().Be("https://example.com");
        update.Snippet.Should().Be("Snippet");
    }

    [Fact]
    public void ClarificationUpdate_SetsValues()
    {
        // Arrange
        var questions = new[] { "Q1?", "Q2?" };

        // Act
        var update = new ClarificationUpdate(questions, "Rationale");

        // Assert
        update.Questions.Should().BeEquivalentTo(questions);
        update.Rationale.Should().Be("Rationale");
    }

    [Fact]
    public void ReflectionUpdate_SetsValues()
    {
        // Act
        var update = new ReflectionUpdate(0.85, "Good", 2);

        // Assert
        update.ConfidenceScore.Should().Be(0.85);
        update.Reasoning.Should().Be("Good");
        update.Iterations.Should().Be(2);
    }

    [Fact]
    public void GoogleSearchResponse_SetsValues()
    {
        // Arrange
        var items = new[] { new GoogleSearchItem("Title", "Link", "Snippet", null) };
        var searchInfo = new SearchInformation(0.5, "100");

        // Act
        var response = new GoogleSearchResponse(searchInfo, items);

        // Assert
        response.SearchInformation.Should().NotBeNull();
        response.Items.Should().HaveCount(1);
    }

    [Fact]
    public void SearchInformation_SetsValues()
    {
        // Act
        var info = new SearchInformation(0.25, "1000");

        // Assert
        info.SearchTime.Should().Be(0.25);
        info.TotalResults.Should().Be("1000");
    }

    [Fact]
    public void GoogleSearchItem_SetsValues()
    {
        // Act
        var item = new GoogleSearchItem("Title", "https://example.com", "Snippet", "<b>Html</b>");

        // Assert
        item.Title.Should().Be("Title");
        item.Link.Should().Be("https://example.com");
        item.Snippet.Should().Be("Snippet");
        item.HtmlSnippet.Should().Be("<b>Html</b>");
    }

    [Fact]
    public void StreamTokenTypes_ContainsExpectedValues()
    {
        // Assert
        StreamTokenTypes.Content.Should().Be("content");
        StreamTokenTypes.Progress.Should().Be("progress");
        StreamTokenTypes.Plan.Should().Be("plan");
        StreamTokenTypes.SearchQuery.Should().Be("search_query");
        StreamTokenTypes.Source.Should().Be("source");
        StreamTokenTypes.Clarification.Should().Be("clarification");
        StreamTokenTypes.Reflection.Should().Be("reflection");
        StreamTokenTypes.SourcesComplete.Should().Be("sources_complete");
        StreamTokenTypes.Fallback.Should().Be("fallback");
        StreamTokenTypes.Error.Should().Be("error");
    }
}

public class AgentModelsTests
{
    [Fact]
    public void ResearchPlan_DefaultValues_AreEmpty()
    {
        // Act
        var plan = new ResearchPlan();

        // Assert
        plan.MainGoal.Should().BeEmpty();
        plan.Subtasks.Should().BeEmpty();
        plan.KeyConcepts.Should().BeEmpty();
    }

    [Fact]
    public void ResearchTask_DefaultValues_AreEmpty()
    {
        // Act
        var task = new ResearchTask();

        // Assert
        task.Description.Should().BeEmpty();
        task.SearchQuery.Should().BeEmpty();
        task.Priority.Should().Be(0);
    }

    [Fact]
    public void GatheredInformation_DefaultValues_AreEmpty()
    {
        // Act
        var info = new GatheredInformation();

        // Assert
        info.Results.Should().BeEmpty();
        info.StoredMemoryIds.Should().BeEmpty();
        info.TotalSourcesFound.Should().Be(0);
    }

    [Fact]
    public void ReflectionResult_DefaultValues_AreEmpty()
    {
        // Act
        var result = new ReflectionResult();

        // Assert
        result.ConfidenceScore.Should().Be(0);
        result.IdentifiedGaps.Should().BeEmpty();
        result.SuggestedAdditionalSearches.Should().BeEmpty();
        result.RequiresMoreResearch.Should().BeFalse();
    }

    [Fact]
    public void ClarificationResult_DefaultValues_AreEmpty()
    {
        // Act
        var result = new ClarificationResult();

        // Assert
        result.Questions.Should().BeEmpty();
        result.Rationale.Should().BeEmpty();
    }
}
