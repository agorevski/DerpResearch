using DeepResearch.WebApp.Memory;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DerpResearch.Tests.Unit.Memory;

public class PersistentFaissIndexTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly DatabaseInitializer _dbInitializer;
    private readonly Mock<ILogger<PersistentFaissIndex>> _mockLogger;

    public PersistentFaissIndexTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_vectors_{Guid.NewGuid()}.db");
        var mockDbLogger = new Mock<ILogger<DatabaseInitializer>>();
        _dbInitializer = new DatabaseInitializer(_testDbPath, mockDbLogger.Object);
        _mockLogger = new Mock<ILogger<PersistentFaissIndex>>();
    }

    public void Dispose()
    {
        // Force garbage collection to close any open connections
        GC.Collect();
        GC.WaitForPendingFinalizers();
        
        // Small delay to ensure SQLite releases the file
        Thread.Sleep(100);
        
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch (IOException)
        {
            // If file is still locked, just leave it - temp directory will be cleaned up eventually
        }
    }

    [Fact]
    public async Task AddVectorAsync_ShouldPersistToDatabase()
    {
        // Arrange
        await _dbInitializer.InitializeAsync();
        var index = new PersistentFaissIndex(dimension: 3, logger: _mockLogger.Object);
        var embedding = new float[] { 1.0f, 2.0f, 3.0f };

        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        // Act
        var vectorId = await index.AddVectorAsync(embedding, connection);

        // Assert
        Assert.Equal(0, vectorId); // First vector should have ID 0
        Assert.Equal(1, index.Count);

        // Verify it's in the database
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM VectorStore WHERE VectorId = $id";
        command.Parameters.AddWithValue("$id", vectorId);
        var count = (long)(await command.ExecuteScalarAsync())!;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AddVectorAsync_WithIncorrectDimension_ShouldThrowException()
    {
        // Arrange
        await _dbInitializer.InitializeAsync();
        var index = new PersistentFaissIndex(dimension: 3, logger: _mockLogger.Object);
        var wrongEmbedding = new float[] { 1.0f, 2.0f }; // Only 2 dimensions

        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await index.AddVectorAsync(wrongEmbedding, connection));
    }

    [Fact]
    public async Task LoadFromDatabaseAsync_ShouldRestoreVectors()
    {
        // Arrange
        await _dbInitializer.InitializeAsync();
        var index1 = new PersistentFaissIndex(dimension: 3, logger: _mockLogger.Object);
        var embedding1 = new float[] { 1.0f, 2.0f, 3.0f };
        var embedding2 = new float[] { 4.0f, 5.0f, 6.0f };

        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        // Add vectors with first index
        await index1.AddVectorAsync(embedding1, connection);
        await index1.AddVectorAsync(embedding2, connection);

        // Act - Create new index and load from database
        var index2 = new PersistentFaissIndex(dimension: 3, logger: _mockLogger.Object);
        await index2.LoadFromDatabaseAsync(connection);

        // Assert
        Assert.Equal(2, index2.Count);

        // Verify we can search with loaded vectors
        var query = new float[] { 1.5f, 2.5f, 3.5f };
        var (ids, distances) = await index2.SearchAsync(query, topK: 2);
        Assert.Equal(2, ids.Length);
        Assert.Equal(2, distances.Length);
    }

    [Fact]
    public async Task Search_ShouldReturnMostSimilarVectors()
    {
        // Arrange
        await _dbInitializer.InitializeAsync();
        var index = new PersistentFaissIndex(dimension: 3, logger: _mockLogger.Object);

        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        // Add vectors
        var vector1 = new float[] { 1.0f, 0.0f, 0.0f };
        var vector2 = new float[] { 0.0f, 1.0f, 0.0f };
        var vector3 = new float[] { 0.9f, 0.1f, 0.0f }; // Very similar to vector1

        await index.AddVectorAsync(vector1, connection);
        await index.AddVectorAsync(vector2, connection);
        await index.AddVectorAsync(vector3, connection);

        // Act - Search for vector similar to vector1
        var query = new float[] { 1.0f, 0.0f, 0.0f };
        var (ids, distances) = await index.SearchAsync(query, topK: 2);

        // Assert
        Assert.Equal(2, ids.Length);
        Assert.Equal(0, ids[0]); // vector1 should be most similar (exact match)
        Assert.Equal(2, ids[1]); // vector3 should be second most similar
        Assert.True(distances[0] >= distances[1]); // Similarities should be descending
    }

    [Fact]
    public async Task Search_WithEmptyIndex_ShouldReturnEmpty()
    {
        // Arrange
        await _dbInitializer.InitializeAsync();
        var index = new PersistentFaissIndex(dimension: 3, logger: _mockLogger.Object);
        var query = new float[] { 1.0f, 2.0f, 3.0f };

        // Act
        var (ids, distances) = await index.SearchAsync(query, topK: 5);

        // Assert
        Assert.Empty(ids);
        Assert.Empty(distances);
    }

    [Fact]
    public async Task Search_WithTopKLargerThanCount_ShouldReturnAllVectors()
    {
        // Arrange
        await _dbInitializer.InitializeAsync();
        var index = new PersistentFaissIndex(dimension: 3, logger: _mockLogger.Object);

        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        await index.AddVectorAsync(new float[] { 1.0f, 0.0f, 0.0f }, connection);
        await index.AddVectorAsync(new float[] { 0.0f, 1.0f, 0.0f }, connection);

        // Act - Request more vectors than available
        var query = new float[] { 1.0f, 0.0f, 0.0f };
        var (ids, distances) = await index.SearchAsync(query, topK: 10);

        // Assert
        Assert.Equal(2, ids.Length); // Should only return 2 vectors
        Assert.Equal(2, distances.Length);
    }

    [Fact]
    public async Task LoadFromDatabaseAsync_ShouldSkipInvalidDimensions()
    {
        // Arrange
        await _dbInitializer.InitializeAsync();
        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        // Manually insert a vector with wrong dimension
        var wrongDimensionBytes = new byte[2 * sizeof(float)]; // 2 dimensions
        Buffer.BlockCopy(new float[] { 1.0f, 2.0f }, 0, wrongDimensionBytes, 0, wrongDimensionBytes.Length);

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO VectorStore (VectorId, Embedding, Dimension, CreatedAt)
            VALUES (999, $embedding, 2, $createdAt)
        ";
        command.Parameters.AddWithValue("$embedding", wrongDimensionBytes);
        command.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();

        // Act - Load with dimension 3 (should skip the 2-dimension vector)
        var index = new PersistentFaissIndex(dimension: 3, logger: _mockLogger.Object);
        await index.LoadFromDatabaseAsync(connection);

        // Assert
        Assert.Equal(0, index.Count); // Should have skipped the invalid vector
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Skipping vector")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LoadFromDatabaseAsync_ShouldSetNextIdCorrectly()
    {
        // Arrange
        await _dbInitializer.InitializeAsync();
        var index1 = new PersistentFaissIndex(dimension: 3, logger: _mockLogger.Object);

        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        // Add some vectors
        await index1.AddVectorAsync(new float[] { 1.0f, 0.0f, 0.0f }, connection);
        await index1.AddVectorAsync(new float[] { 0.0f, 1.0f, 0.0f }, connection);

        // Act - Load into new index and add another vector
        var index2 = new PersistentFaissIndex(dimension: 3, logger: _mockLogger.Object);
        await index2.LoadFromDatabaseAsync(connection);
        var newVectorId = await index2.AddVectorAsync(new float[] { 0.0f, 0.0f, 1.0f }, connection);

        // Assert
        Assert.Equal(2, newVectorId); // Should be 2 (next after 0 and 1)
        Assert.Equal(3, index2.Count);
    }

    [Fact]
    public void Clear_ShouldClearInMemoryVectors()
    {
        // Arrange
        var index = new PersistentFaissIndex(dimension: 3, logger: _mockLogger.Object);

        // Act
        index.Clear();

        // Assert
        Assert.Equal(0, index.Count);
    }

    [Fact]
    public async Task CosineSimilarity_ShouldComputeCorrectly()
    {
        // Arrange
        await _dbInitializer.InitializeAsync();
        var index = new PersistentFaissIndex(dimension: 3, logger: _mockLogger.Object);

        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        // Two identical vectors should have similarity of 1.0
        var vector1 = new float[] { 1.0f, 0.0f, 0.0f };
        await index.AddVectorAsync(vector1, connection);

        // Act
        var (ids, distances) = await index.SearchAsync(vector1, topK: 1);

        // Assert
        Assert.Single(ids);
        Assert.InRange(distances[0], 0.999f, 1.001f); // Should be very close to 1.0
    }

    [Fact]
    public async Task MultipleConnections_ShouldWorkCorrectly()
    {
        // Arrange
        await _dbInitializer.InitializeAsync();
        var index = new PersistentFaissIndex(dimension: 3, logger: _mockLogger.Object);

        // Act - Add vectors using different connections
        await using (var connection1 = _dbInitializer.CreateConnection())
        {
            await connection1.OpenAsync();
            await index.AddVectorAsync(new float[] { 1.0f, 0.0f, 0.0f }, connection1);
        }

        await using (var connection2 = _dbInitializer.CreateConnection())
        {
            await connection2.OpenAsync();
            await index.AddVectorAsync(new float[] { 0.0f, 1.0f, 0.0f }, connection2);
        }

        // Assert
        Assert.Equal(2, index.Count);
    }

    [Fact]
    public async Task PersistenceRoundTrip_ShouldPreserveVectorData()
    {
        // Arrange
        await _dbInitializer.InitializeAsync();
        var index1 = new PersistentFaissIndex(dimension: 3072, logger: _mockLogger.Object); // Real dimension

        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        // Create a realistic embedding
        var originalEmbedding = new float[3072];
        for (int i = 0; i < 3072; i++)
        {
            originalEmbedding[i] = (float)Math.Sin(i * 0.01);
        }

        // Act - Add and reload
        var vectorId = await index1.AddVectorAsync(originalEmbedding, connection);
        var index2 = new PersistentFaissIndex(dimension: 3072, logger: _mockLogger.Object);
        await index2.LoadFromDatabaseAsync(connection);

        // Search with original embedding - should find itself with perfect similarity
        var (ids, distances) = await index2.SearchAsync(originalEmbedding, topK: 1);

        // Assert
        Assert.Single(ids);
        Assert.Equal(vectorId, ids[0]);
        Assert.InRange(distances[0], 0.999f, 1.001f); // Near-perfect similarity
    }

    [Fact]
    public async Task SearchAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        await _dbInitializer.InitializeAsync();
        var index = new PersistentFaissIndex(dimension: 100, logger: _mockLogger.Object);

        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        // Add many vectors to make search take longer
        for (int i = 0; i < 1000; i++)
        {
            var embedding = new float[100];
            for (int j = 0; j < 100; j++)
            {
                embedding[j] = (float)(i + j * 0.1);
            }
            await index.AddVectorAsync(embedding, connection);
        }

        var query = new float[100];
        for (int j = 0; j < 100; j++)
        {
            query[j] = (float)(j * 0.5);
        }

        // Act - Cancel immediately
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Assert - Should throw OperationCanceledException (or derived TaskCanceledException)
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await index.SearchAsync(query, topK: 10, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task SearchAsync_ConcurrentSearches_ShouldReturnConsistentResults()
    {
        // Arrange
        await _dbInitializer.InitializeAsync();
        var index = new PersistentFaissIndex(dimension: 3, logger: _mockLogger.Object);

        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        // Add test vectors
        await index.AddVectorAsync(new float[] { 1.0f, 0.0f, 0.0f }, connection);
        await index.AddVectorAsync(new float[] { 0.0f, 1.0f, 0.0f }, connection);
        await index.AddVectorAsync(new float[] { 0.0f, 0.0f, 1.0f }, connection);

        var query1 = new float[] { 1.0f, 0.0f, 0.0f };
        var query2 = new float[] { 0.0f, 1.0f, 0.0f };

        // Act - Run concurrent searches
        var task1 = index.SearchAsync(query1, topK: 2);
        var task2 = index.SearchAsync(query2, topK: 2);
        var task3 = index.SearchAsync(query1, topK: 2);

        await Task.WhenAll(task1, task2, task3);

        // Assert - Results should be consistent
        var result1 = await task1;
        var result2 = await task2;
        var result3 = await task3;

        Assert.Equal(2, result1.ids.Length);
        Assert.Equal(2, result2.ids.Length);
        Assert.Equal(2, result3.ids.Length);

        // Same query should give same results
        Assert.Equal(result1.ids[0], result3.ids[0]);
        Assert.Equal(result1.ids[1], result3.ids[1]);
        Assert.Equal(result1.distances[0], result3.distances[0], precision: 5);
    }

    [Fact]
    public async Task SearchAsync_WithIncorrectDimension_ShouldThrowException()
    {
        // Arrange
        await _dbInitializer.InitializeAsync();
        var index = new PersistentFaissIndex(dimension: 3, logger: _mockLogger.Object);

        await using var connection = _dbInitializer.CreateConnection();
        await connection.OpenAsync();

        await index.AddVectorAsync(new float[] { 1.0f, 0.0f, 0.0f }, connection);

        var wrongQuery = new float[] { 1.0f, 0.0f }; // Only 2 dimensions

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await index.SearchAsync(wrongQuery, topK: 1));
    }
}
