namespace DeepResearch.WebApp.Models;

public class MemoryChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public int? VectorId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? ConversationId { get; set; }
    public float RelevanceScore { get; set; }
}

public class SearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string? Content { get; set; } // Full webpage content
}

public class ConversationContext
{
    public string ConversationId { get; set; } = string.Empty;
    public ChatMessage[] RecentMessages { get; set; } = Array.Empty<ChatMessage>();
    public MemoryChunk[] RelevantMemories { get; set; } = Array.Empty<MemoryChunk>();
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty; // "system", "user", "assistant"
    public string Content { get; set; } = string.Empty;
}

public class Conversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Message
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConversationId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of a memory storage operation with success/failure tracking per chunk.
/// Prevents silent failures by exposing chunk-level errors.
/// </summary>
public class StoreMemoryResult
{
    public string PrimaryId { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public int SuccessfulChunks { get; set; }
    public int FailedChunks { get; set; }
    public List<ChunkError> Errors { get; set; } = new();
    
    public bool IsFullySuccessful => FailedChunks == 0 && TotalChunks > 0;
    public bool IsPartiallySuccessful => SuccessfulChunks > 0 && FailedChunks > 0;
    public bool IsCompleteFailure => SuccessfulChunks == 0 && TotalChunks > 0;
    
    public static StoreMemoryResult Empty() => new() { TotalChunks = 0, SuccessfulChunks = 0 };
}

/// <summary>
/// Represents an error that occurred while storing a specific chunk.
/// </summary>
public class ChunkError
{
    public int ChunkIndex { get; set; }
    public string ChunkId { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
}
