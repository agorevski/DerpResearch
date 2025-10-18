using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace DeepResearch.WebApp.Memory;

public class DatabaseInitializer
{
    private readonly string _connectionString;
    private readonly string _databasePath;
    private readonly ILogger<DatabaseInitializer>? _logger;

    public DatabaseInitializer(string databasePath, ILogger<DatabaseInitializer>? logger = null)
    {
        _databasePath = databasePath;
        _connectionString = $"Data Source={databasePath}";
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger?.LogInformation("Starting database initialization...");
            _logger?.LogInformation("Database path: {Path}", _databasePath);
            
            // Check directory existence and permissions
            var directory = Path.GetDirectoryName(_databasePath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException($"Invalid database path: {_databasePath}");
            }

            _logger?.LogInformation("Database directory: {Directory}", directory);
            
            if (!Directory.Exists(directory))
            {
                _logger?.LogWarning("Database directory does not exist, attempting to create: {Directory}", directory);
                Directory.CreateDirectory(directory);
                _logger?.LogInformation("Database directory created successfully");
            }
            else
            {
                _logger?.LogInformation("Database directory exists");
            }

            // Test write permissions
            var testFile = Path.Combine(directory, ".write_test");
            try
            {
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                _logger?.LogInformation("Write permissions verified for directory");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "No write permissions for directory: {Directory}", directory);
                throw new InvalidOperationException($"Cannot write to directory: {directory}", ex);
            }

            _logger?.LogInformation("Opening SQLite connection...");
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            _logger?.LogInformation("SQLite connection opened successfully");

        var createTablesCommand = connection.CreateCommand();
        createTablesCommand.CommandText = @"
            -- Conversations table
            CREATE TABLE IF NOT EXISTS Conversations (
                Id TEXT PRIMARY KEY,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            -- Messages table
            CREATE TABLE IF NOT EXISTS Messages (
                Id TEXT PRIMARY KEY,
                ConversationId TEXT NOT NULL,
                Role TEXT NOT NULL,
                Content TEXT NOT NULL,
                Timestamp TEXT NOT NULL,
                FOREIGN KEY(ConversationId) REFERENCES Conversations(Id)
            );

            -- Memories table
            CREATE TABLE IF NOT EXISTS Memories (
                Id TEXT PRIMARY KEY,
                Text TEXT NOT NULL,
                Source TEXT,
                Tags TEXT,
                VectorId INTEGER,
                Timestamp TEXT NOT NULL,
                ConversationId TEXT
            );

            -- Search cache table
            CREATE TABLE IF NOT EXISTS SearchCache (
                QueryHash TEXT PRIMARY KEY,
                Results TEXT NOT NULL,
                Timestamp TEXT NOT NULL
            );

            -- Create indexes for performance
            CREATE INDEX IF NOT EXISTS idx_messages_conversation 
                ON Messages(ConversationId, Timestamp);
            
            CREATE INDEX IF NOT EXISTS idx_memories_conversation 
                ON Memories(ConversationId, Timestamp);
            
            CREATE INDEX IF NOT EXISTS idx_memories_timestamp 
                ON Memories(Timestamp);
            
            CREATE INDEX IF NOT EXISTS idx_search_cache_timestamp 
                ON SearchCache(Timestamp);
        ";

        _logger?.LogInformation("Executing table creation commands...");
        await createTablesCommand.ExecuteNonQueryAsync();
        _logger?.LogInformation("Database tables created/verified successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Database initialization failed: {Message}", ex.Message);
            throw;
        }
    }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}
