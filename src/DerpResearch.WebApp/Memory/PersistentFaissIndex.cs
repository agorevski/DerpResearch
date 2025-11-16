using Microsoft.Data.Sqlite;

namespace DeepResearch.WebApp.Memory;

/// <summary>
/// Persistent vector search using cosine similarity with SQLite BLOB storage
/// Vectors are stored in the database and loaded on startup to prevent data loss
/// </summary>
public class PersistentFaissIndex
{
    private readonly int _dimension;
    private readonly Dictionary<int, float[]> _vectors;
    private int _nextId;
    private readonly object _lock = new();
    private readonly ILogger<PersistentFaissIndex>? _logger;

    public PersistentFaissIndex(int dimension = 3072, ILogger<PersistentFaissIndex>? logger = null)
    {
        _dimension = dimension;
        _vectors = new Dictionary<int, float[]>();
        _nextId = 0;
        _logger = logger;
    }

    /// <summary>
    /// Load all vectors from database on startup
    /// </summary>
    public async Task LoadFromDatabaseAsync(SqliteConnection connection)
    {
        _logger?.LogInformation("Loading vectors from database...");
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT VectorId, Embedding
            FROM VectorStore
            WHERE Embedding IS NOT NULL
            ORDER BY VectorId
        ";

        var loadedCount = 0;
        var tempVectors = new Dictionary<int, float[]>();
        var maxId = 0;

        // Read all data outside the lock
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var vectorId = reader.GetInt32(0);
            var embeddingBytes = (byte[])reader.GetValue(1);
            var embedding = BytesToFloatArray(embeddingBytes);

            if (embedding.Length != _dimension)
            {
                _logger?.LogWarning("Skipping vector {VectorId} with incorrect dimension {Dimension}, expected {Expected}",
                    vectorId, embedding.Length, _dimension);
                continue;
            }

            tempVectors[vectorId] = embedding;
            loadedCount++;

            // Track highest ID for next insert
            if (vectorId > maxId)
            {
                maxId = vectorId;
            }
        }

        // Update in-memory state inside lock
        lock (_lock)
        {
            foreach (var kvp in tempVectors)
            {
                _vectors[kvp.Key] = kvp.Value;
            }
            _nextId = maxId + 1;
        }

        _logger?.LogInformation("Loaded {Count} vectors from database. Next ID: {NextId}", 
            loadedCount, _nextId);
    }

    /// <summary>
    /// Add a vector and persist it to the database
    /// </summary>
    public async Task<int> AddVectorAsync(float[] embedding, SqliteConnection connection)
    {
        if (embedding.Length != _dimension)
        {
            throw new ArgumentException($"Expected dimension {_dimension}, got {embedding.Length}");
        }

        int id;
        lock (_lock)
        {
            id = _nextId++;
            _vectors[id] = embedding;
        }

        // Persist to database
        var embeddingBytes = FloatArrayToBytes(embedding);
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO VectorStore (VectorId, Embedding, Dimension, CreatedAt)
            VALUES ($vectorId, $embedding, $dimension, $createdAt)
        ";
        command.Parameters.AddWithValue("$vectorId", id);
        command.Parameters.AddWithValue("$embedding", embeddingBytes);
        command.Parameters.AddWithValue("$dimension", _dimension);
        command.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();

        _logger?.LogDebug("Added and persisted vector {VectorId} with dimension {Dimension}", id, _dimension);
        return id;
    }

    /// <summary>
    /// Search for similar vectors using cosine similarity
    /// CPU-bound operation is offloaded to thread pool to avoid blocking async operations
    /// </summary>
    public async Task<(int[] ids, float[] distances)> SearchAsync(
        float[] queryEmbedding, 
        int topK,
        CancellationToken cancellationToken = default)
    {
        if (queryEmbedding.Length != _dimension)
        {
            throw new ArgumentException($"Expected dimension {_dimension}, got {queryEmbedding.Length}");
        }

        if (_vectors.Count == 0)
        {
            return (Array.Empty<int>(), Array.Empty<float>());
        }

        // Offload CPU-bound work to thread pool to avoid blocking the async caller
        return await Task.Run(() =>
        {
            var similarities = new List<(int id, float similarity)>();

            lock (_lock)
            {
                foreach (var kvp in _vectors)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var similarity = CosineSimilarity(queryEmbedding, kvp.Value);
                    similarities.Add((kvp.Key, similarity));
                }
            }

            var topResults = similarities
                .OrderByDescending(x => x.similarity)
                .Take(topK)
                .ToArray();

            var ids = topResults.Select(x => x.id).ToArray();
            var distances = topResults.Select(x => x.similarity).ToArray();

            return (ids, distances);
        }, cancellationToken);
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors
    /// </summary>
    private float CosineSimilarity(float[] a, float[] b)
    {
        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0)
        {
            return 0;
        }

        return (float)(dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB)));
    }

    /// <summary>
    /// Convert float array to byte array for BLOB storage
    /// </summary>
    private byte[] FloatArrayToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// Convert byte array from BLOB storage to float array
    /// </summary>
    private float[] BytesToFloatArray(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    /// <summary>
    /// Get current vector count
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _vectors.Count;
            }
        }
    }

    /// <summary>
    /// Clear all vectors (in-memory only, does not affect database)
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _vectors.Clear();
            _nextId = 0;
        }
    }
}
