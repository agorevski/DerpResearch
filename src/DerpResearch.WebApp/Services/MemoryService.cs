using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DeepResearch.WebApp.Memory;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace DeepResearch.WebApp.Services;

public class MemoryService : IMemoryService
{
    private readonly DatabaseInitializer _dbInitializer;
    private readonly SimpleFaissIndex _faissIndex;
    private readonly ILLMService _llmService;
    private readonly ILogger<MemoryService> _logger;
    private readonly int _topK;

    public MemoryService(
        IConfiguration config,
        ILLMService llmService,
        ILogger<MemoryService> logger,
        ILoggerFactory loggerFactory)
    {
        var dbPath = config["Memory:DatabasePath"] ?? "Data/deepresearch.db";
        _logger = logger;
        _logger.LogInformation("Initializing MemoryService with database path: {DbPath}", dbPath);
        
        var dbLogger = loggerFactory.CreateLogger<DatabaseInitializer>();
        _dbInitializer = new DatabaseInitializer(dbPath, dbLogger);
        
        _faissIndex = new SimpleFaissIndex();
        _llmService = llmService;
        _topK = int.Parse(config["Memory:TopKResults"] ?? "5");
        
        _logger.LogInformation("MemoryService constructor completed. TopK: {TopK}", _topK);
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("MemoryService.InitializeAsync() called");
        try
        {
            await _dbInitializer.InitializeAsync();
            _logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MemoryService initialization failed: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<string> StoreMemoryAsync(string text, string source, string[] tags, string? conversationId = null)
    {
        // Split large texts into chunks to avoid embedding token limits
        // Using conservative defaults: 3000 tokens max, 100 token overlap
        var chunks = TextChunker.ChunkText(text, maxTokens: 3000, overlapTokens: 100);
        
        if (chunks.Length == 0)
        {
            _logger.LogWarning("Empty text provided to StoreMemoryAsync from source: {Source}", source);
            return string.Empty;
        }

        _logger.LogInformation("Splitting text into {ChunkCount} chunks for source: {Source}", chunks.Length, source);

        var primaryId = Guid.NewGuid().ToString();

        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        // Store each chunk as a separate memory entry
        for (int i = 0; i < chunks.Length; i++)
        {
            var chunk = chunks[i];
            var chunkId = chunks.Length == 1 ? primaryId : $"{primaryId}-chunk{i}";
            
            try
            {
                var embedding = await _llmService.GetEmbedding(chunk);
                var vectorId = _faissIndex.AddVector(embedding);

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Memories (Id, Text, Source, Tags, VectorId, Timestamp, ConversationId)
                    VALUES ($id, $text, $source, $tags, $vectorId, $timestamp, $conversationId)
                ";
                command.Parameters.AddWithValue("$id", chunkId);
                command.Parameters.AddWithValue("$text", chunk);
                
                // Add chunk info to source if multiple chunks
                var sourceWithChunk = chunks.Length > 1 
                    ? $"{source} (chunk {i + 1}/{chunks.Length})" 
                    : source;
                command.Parameters.AddWithValue("$source", sourceWithChunk);
                
                command.Parameters.AddWithValue("$tags", JsonSerializer.Serialize(tags));
                command.Parameters.AddWithValue("$vectorId", vectorId);
                command.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("O"));
                command.Parameters.AddWithValue("$conversationId", conversationId ?? (object)DBNull.Value);

                await command.ExecuteNonQueryAsync();
                _logger.LogDebug("Stored memory chunk {ChunkId} ({Index}/{Total}) with vector {VectorId}", 
                    chunkId, i + 1, chunks.Length, vectorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store chunk {Index}/{Total} from source {Source}: {Message}", 
                    i + 1, chunks.Length, source, ex.Message);
                // Continue with next chunk even if one fails
            }
        }

        return primaryId;
    }

    public async Task<MemoryChunk[]> SearchMemoryAsync(string query, int topK = 5, string? conversationId = null)
    {
        if (_faissIndex.Count == 0)
        {
            return Array.Empty<MemoryChunk>();
        }

        var queryEmbedding = await _llmService.GetEmbedding(query);
        var (vectorIds, similarities) = _faissIndex.Search(queryEmbedding, topK);

        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        var memories = new List<MemoryChunk>();

        foreach (var (vectorId, similarity) in vectorIds.Zip(similarities))
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Text, Source, Tags, Timestamp, ConversationId
                FROM Memories
                WHERE VectorId = $vectorId
            ";
            command.Parameters.AddWithValue("$vectorId", vectorId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var tags = JsonSerializer.Deserialize<string[]>(reader.GetString(3)) ?? Array.Empty<string>();
                
                memories.Add(new MemoryChunk
                {
                    Id = reader.GetString(0),
                    Text = reader.GetString(1),
                    Source = reader.GetString(2),
                    Tags = tags,
                    Timestamp = DateTime.Parse(reader.GetString(4)),
                    ConversationId = reader.IsDBNull(5) ? null : reader.GetString(5),
                    RelevanceScore = similarity
                });
            }
        }

        return memories
            .OrderByDescending(m => m.RelevanceScore)
            .ToArray();
    }

    public async Task<ConversationContext> GetConversationContextAsync(string conversationId, int messageCount = 10)
    {
        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        // Get recent messages
        var messagesCommand = connection.CreateCommand();
        messagesCommand.CommandText = @"
            SELECT Role, Content
            FROM Messages
            WHERE ConversationId = $conversationId
            ORDER BY Timestamp DESC
            LIMIT $limit
        ";
        messagesCommand.Parameters.AddWithValue("$conversationId", conversationId);
        messagesCommand.Parameters.AddWithValue("$limit", messageCount);

        var messages = new List<ChatMessage>();
        await using var reader = await messagesCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(new ChatMessage
            {
                Role = reader.GetString(0),
                Content = reader.GetString(1)
            });
        }
        messages.Reverse(); // Return in chronological order

        // Get relevant memories for this conversation
        var memoriesCommand = connection.CreateCommand();
        memoriesCommand.CommandText = @"
            SELECT Id, Text, Source, Tags, VectorId, Timestamp
            FROM Memories
            WHERE ConversationId = $conversationId
            ORDER BY Timestamp DESC
            LIMIT 5
        ";
        memoriesCommand.Parameters.AddWithValue("$conversationId", conversationId);

        var memories = new List<MemoryChunk>();
        await using var memReader = await memoriesCommand.ExecuteReaderAsync();
        while (await memReader.ReadAsync())
        {
            var tags = JsonSerializer.Deserialize<string[]>(memReader.GetString(3)) ?? Array.Empty<string>();
            
            memories.Add(new MemoryChunk
            {
                Id = memReader.GetString(0),
                Text = memReader.GetString(1),
                Source = memReader.GetString(2),
                Tags = tags,
                VectorId = memReader.IsDBNull(4) ? null : memReader.GetInt32(4),
                Timestamp = DateTime.Parse(memReader.GetString(5)),
                ConversationId = conversationId
            });
        }

        return new ConversationContext
        {
            ConversationId = conversationId,
            RecentMessages = messages.ToArray(),
            RelevantMemories = memories.ToArray()
        };
    }

    public async Task SaveMessageAsync(string conversationId, string role, string content)
    {
        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Messages (Id, ConversationId, Role, Content, Timestamp)
            VALUES ($id, $conversationId, $role, $content, $timestamp)
        ";
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$conversationId", conversationId);
        command.Parameters.AddWithValue("$role", role);
        command.Parameters.AddWithValue("$content", content);
        command.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task<string> CreateConversationAsync()
    {
        var conversationId = Guid.NewGuid().ToString();

        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Conversations (Id, CreatedAt, UpdatedAt)
            VALUES ($id, $createdAt, $updatedAt)
        ";
        command.Parameters.AddWithValue("$id", conversationId);
        command.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Created conversation {ConversationId}", conversationId);

        return conversationId;
    }

    public async Task CompactMemoriesAsync(DateTime olderThan)
    {
        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM Memories
            WHERE Timestamp < $threshold
        ";
        command.Parameters.AddWithValue("$threshold", olderThan.ToString("O"));

        var deleted = await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Compacted {Count} old memories", deleted);
    }

    public async Task StoreClarificationQuestionsAsync(string conversationId, string[] questions)
    {
        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO ClarificationQuestions (Id, ConversationId, Questions, CreatedAt)
            VALUES ($id, $conversationId, $questions, $createdAt)
        ";
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$conversationId", conversationId);
        command.Parameters.AddWithValue("$questions", JsonSerializer.Serialize(questions));
        command.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Stored {Count} clarification questions for conversation {ConversationId}", 
            questions.Length, conversationId);
    }

    public async Task<string[]?> GetClarificationQuestionsAsync(string conversationId)
    {
        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Questions
            FROM ClarificationQuestions
            WHERE ConversationId = $conversationId
            ORDER BY CreatedAt DESC
            LIMIT 1
        ";
        command.Parameters.AddWithValue("$conversationId", conversationId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var questionsJson = reader.GetString(0);
            var questions = JsonSerializer.Deserialize<string[]>(questionsJson);
            _logger.LogInformation("Retrieved {Count} clarification questions for conversation {ConversationId}", 
                questions?.Length ?? 0, conversationId);
            return questions;
        }

        _logger.LogInformation("No clarification questions found for conversation {ConversationId}", conversationId);
        return null;
    }

    public async Task ClearClarificationQuestionsAsync(string conversationId)
    {
        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM ClarificationQuestions
            WHERE ConversationId = $conversationId
        ";
        command.Parameters.AddWithValue("$conversationId", conversationId);

        var deleted = await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Cleared {Count} clarification question record(s) for conversation {ConversationId}", 
            deleted, conversationId);
    }
}
