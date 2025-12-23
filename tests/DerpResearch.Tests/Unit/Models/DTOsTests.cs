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

public class EntitiesTests
{
    [Fact]
    public void MemoryChunk_DefaultValues_AreSet()
    {
        // Act
        var chunk = new MemoryChunk();

        // Assert
        chunk.Id.Should().NotBeNullOrEmpty();
        chunk.Text.Should().BeEmpty();
        chunk.Source.Should().BeEmpty();
        chunk.Tags.Should().BeEmpty();
        chunk.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SearchResult_DefaultValues_AreEmpty()
    {
        // Act
        var result = new SearchResult();

        // Assert
        result.Title.Should().BeEmpty();
        result.Url.Should().BeEmpty();
        result.Snippet.Should().BeEmpty();
        result.Content.Should().BeNull();
    }

    [Fact]
    public void ConversationContext_DefaultValues_AreEmpty()
    {
        // Act
        var context = new ConversationContext();

        // Assert
        context.ConversationId.Should().BeEmpty();
        context.RecentMessages.Should().BeEmpty();
        context.RelevantMemories.Should().BeEmpty();
    }

    [Fact]
    public void ChatMessage_DefaultValues_AreEmpty()
    {
        // Act
        var message = new ChatMessage();

        // Assert
        message.Role.Should().BeEmpty();
        message.Content.Should().BeEmpty();
    }

    [Fact]
    public void Conversation_DefaultValues_AreSet()
    {
        // Act
        var conversation = new Conversation();

        // Assert
        conversation.Id.Should().NotBeNullOrEmpty();
        conversation.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        conversation.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Message_DefaultValues_AreSet()
    {
        // Act
        var message = new Message();

        // Assert
        message.Id.Should().NotBeNullOrEmpty();
        message.ConversationId.Should().BeEmpty();
        message.Role.Should().BeEmpty();
        message.Content.Should().BeEmpty();
        message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void StoreMemoryResult_IsFullySuccessful_WhenNoFailures()
    {
        // Act
        var result = new StoreMemoryResult
        {
            TotalChunks = 5,
            SuccessfulChunks = 5,
            FailedChunks = 0
        };

        // Assert
        result.IsFullySuccessful.Should().BeTrue();
        result.IsPartiallySuccessful.Should().BeFalse();
        result.IsCompleteFailure.Should().BeFalse();
    }

    [Fact]
    public void StoreMemoryResult_IsPartiallySuccessful_WhenSomeFailures()
    {
        // Act
        var result = new StoreMemoryResult
        {
            TotalChunks = 5,
            SuccessfulChunks = 3,
            FailedChunks = 2
        };

        // Assert
        result.IsFullySuccessful.Should().BeFalse();
        result.IsPartiallySuccessful.Should().BeTrue();
        result.IsCompleteFailure.Should().BeFalse();
    }

    [Fact]
    public void StoreMemoryResult_IsCompleteFailure_WhenAllFail()
    {
        // Act
        var result = new StoreMemoryResult
        {
            TotalChunks = 5,
            SuccessfulChunks = 0,
            FailedChunks = 5
        };

        // Assert
        result.IsFullySuccessful.Should().BeFalse();
        result.IsPartiallySuccessful.Should().BeFalse();
        result.IsCompleteFailure.Should().BeTrue();
    }

    [Fact]
    public void StoreMemoryResult_Empty_CreatesEmptyResult()
    {
        // Act
        var result = StoreMemoryResult.Empty();

        // Assert
        result.TotalChunks.Should().Be(0);
        result.SuccessfulChunks.Should().Be(0);
        result.IsFullySuccessful.Should().BeFalse();
    }
}

public class LLMModelsTests
{
    [Fact]
    public void LLMRequest_SetsDefaultValues()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Hello" } };

        // Act
        var request = new LLMRequest { Messages = messages };

        // Assert
        request.Messages.Should().HaveCount(1);
        request.ModelName.Should().Be("gpt-4o");
        request.Temperature.Should().Be(0.7f);
        request.MaxTokens.Should().BeNull();
    }

    [Fact]
    public void LLMRequest_WithAllValues_SetsAll()
    {
        // Arrange
        var messages = new[] { new ChatMessage { Role = "user", Content = "Hello" } };

        // Act
        var request = new LLMRequest
        {
            Messages = messages,
            ModelName = "gpt-4o-mini",
            Temperature = 0.5f,
            MaxTokens = 1000
        };

        // Assert
        request.ModelName.Should().Be("gpt-4o-mini");
        request.Temperature.Should().Be(0.5f);
        request.MaxTokens.Should().Be(1000);
    }

    [Fact]
    public void LLMResponse_SetsContent()
    {
        // Act
        var response = new LLMResponse
        {
            Content = "Hello World",
            Model = "gpt-4o"
        };

        // Assert
        response.Content.Should().Be("Hello World");
        response.Model.Should().Be("gpt-4o");
        response.Usage.Should().BeNull();
    }

    [Fact]
    public void LLMResponse_WithUsage_SetsAll()
    {
        // Act
        var response = new LLMResponse
        {
            Content = "Hello",
            Usage = new TokenUsage
            {
                PromptTokens = 10,
                CompletionTokens = 5,
                TotalTokens = 15
            }
        };

        // Assert
        response.Usage.Should().NotBeNull();
        response.Usage!.PromptTokens.Should().Be(10);
        response.Usage.CompletionTokens.Should().Be(5);
        response.Usage.TotalTokens.Should().Be(15);
    }

    [Fact]
    public void TokenUsage_DefaultValues()
    {
        // Act
        var usage = new TokenUsage();

        // Assert
        usage.PromptTokens.Should().Be(0);
        usage.CompletionTokens.Should().Be(0);
        usage.TotalTokens.Should().Be(0);
    }
}
