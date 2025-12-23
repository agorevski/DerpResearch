using DeepResearch.WebApp.Models;

namespace DerpResearch.Tests.Helpers;

/// <summary>
/// Builder class for creating test data objects with sensible defaults
/// </summary>
public static class TestDataBuilder
{
    public static ChatRequest CreateChatRequest(
        string prompt = "Test query",
        string mode = "deep-research",
        string? conversationId = null,
        int derpificationLevel = 100,
        string[]? clarificationAnswers = null)
    {
        return new ChatRequest(
            prompt,
            mode,
            conversationId,
            derpificationLevel,
            clarificationAnswers
        );
    }

    public static ConversationContext CreateConversationContext(
        string conversationId = "test-conv-123",
        int messageCount = 2)
    {
        var messages = new List<ChatMessage>();
        
        for (int i = 0; i < messageCount; i++)
        {
            messages.Add(new ChatMessage
            {
                Role = i % 2 == 0 ? "user" : "assistant",
                Content = $"Message {i + 1}"
            });
        }

        return new ConversationContext
        {
            ConversationId = conversationId,
            RecentMessages = messages.ToArray()
        };
    }

    public static ResearchPlan CreateResearchPlan(
        string goal = "Research test topic",
        int subtaskCount = 3)
    {
        var subtasks = new List<ResearchTask>();
        for (int i = 0; i < subtaskCount; i++)
        {
            subtasks.Add(new ResearchTask
            {
                Description = $"Subtask {i + 1}: Search for information",
                SearchQuery = $"search query {i + 1}",
                Priority = i + 1
            });
        }

        return new ResearchPlan
        {
            MainGoal = goal,
            Subtasks = subtasks.ToArray()
        };
    }

    public static SearchResult CreateSearchResult(
        string title = "Test Article",
        string url = "https://example.com/article",
        string snippet = "This is a test snippet")
    {
        return new SearchResult
        {
            Title = title,
            Url = url,
            Snippet = snippet
        };
    }

    public static List<SearchResult> CreateSearchResults(int count = 5)
    {
        var results = new List<SearchResult>();
        for (int i = 0; i < count; i++)
        {
            results.Add(CreateSearchResult(
                $"Article {i + 1}",
                $"https://example.com/article{i + 1}",
                $"Snippet for article {i + 1}"
            ));
        }
        return results;
    }

    public static ReflectionResult CreateReflectionResult(
        float confidence = 0.85f,
        string[]? gaps = null,
        string[]? additionalSearches = null,
        bool requiresMoreResearch = false)
    {
        return new ReflectionResult
        {
            ConfidenceScore = confidence,
            IdentifiedGaps = gaps ?? Array.Empty<string>(),
            SuggestedAdditionalSearches = additionalSearches ?? Array.Empty<string>(),
            RequiresMoreResearch = requiresMoreResearch
        };
    }

    public static ClarificationResult CreateClarificationResult(
        int questionCount = 2,
        string rationale = "Need more context")
    {
        var questions = new List<string>();
        for (int i = 0; i < questionCount; i++)
        {
            questions.Add($"Question {i + 1}?");
        }

        return new ClarificationResult
        {
            Questions = questions.ToArray(),
            Rationale = rationale
        };
    }

    public static MemoryChunk CreateMemoryChunk(
        string content = "Test memory content",
        string conversationId = "test-conv-123",
        string[]? tags = null)
    {
        return new MemoryChunk
        {
            Id = Guid.NewGuid().ToString(),
            Text = content,
            ConversationId = conversationId,
            Tags = tags ?? new[] { "test", "memory" },
            Timestamp = DateTime.UtcNow
        };
    }

    public static float[] CreateTestEmbedding(int dimensions = 1536)
    {
        var embedding = new float[dimensions];
        var random = new Random(42); // Fixed seed for reproducibility
        
        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // Range: -1 to 1
        }
        
        return embedding;
    }

    public static ProgressUpdate CreateProgressUpdate(
        string message = "Processing...",
        string stage = "search")
    {
        return new ProgressUpdate(stage, message);
    }

    public static SourceUpdate CreateSourceUpdate(
        string title = "Test Source",
        string url = "https://example.com",
        string? snippet = "Test snippet")
    {
        return new SourceUpdate(title, url, snippet);
    }

    public static SearchQueryUpdate CreateSearchQueryUpdate(
        string query = "test query",
        int taskNumber = 1,
        int totalTasks = 3)
    {
        return new SearchQueryUpdate(query, taskNumber, totalTasks);
    }

    public static ReflectionUpdate CreateReflectionUpdate(
        double confidence = 0.85,
        string reasoning = "Good quality",
        int iterations = 1)
    {
        return new ReflectionUpdate(confidence, reasoning, iterations);
    }

    public static ClarificationUpdate CreateClarificationUpdate(
        string[] questions,
        string rationale = "Need clarification")
    {
        return new ClarificationUpdate(questions, rationale);
    }

    public static StoreMemoryResult CreateStoreMemoryResult(
        string primaryId = "test-memory-id",
        int totalChunks = 1,
        int successfulChunks = 1,
        int failedChunks = 0,
        List<ChunkError>? errors = null)
    {
        return new StoreMemoryResult
        {
            PrimaryId = primaryId,
            TotalChunks = totalChunks,
            SuccessfulChunks = successfulChunks,
            FailedChunks = failedChunks,
            Errors = errors ?? new List<ChunkError>()
        };
    }

    public static StoreMemoryResult CreateFailedStoreMemoryResult(
        string primaryId = "test-memory-id",
        int totalChunks = 1,
        string errorMessage = "Storage failed")
    {
        return new StoreMemoryResult
        {
            PrimaryId = primaryId,
            TotalChunks = totalChunks,
            SuccessfulChunks = 0,
            FailedChunks = totalChunks,
            Errors = Enumerable.Range(0, totalChunks).Select(i => new ChunkError
            {
                ChunkIndex = i,
                ChunkId = $"{primaryId}-chunk{i}",
                ErrorMessage = errorMessage,
                ExceptionType = "Exception"
            }).ToList()
        };
    }
}
