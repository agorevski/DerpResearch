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
