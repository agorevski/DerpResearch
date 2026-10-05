using System.Collections.Concurrent;

namespace DeepResearch.WebApp.Memory;

/// <summary>
/// Simple in-memory vector search using cosine similarity
/// This is a lightweight alternative to FAISS for pure C# implementation
/// For production, consider integrating actual FAISS via P/Invoke or separate process
/// </summary>
public class SimpleFaissIndex
{
    private readonly int _dimension;
    private readonly ConcurrentDictionary<int, float[]> _vectors;
    private int _nextId;
    private readonly object _lock = new();

    public SimpleFaissIndex(int dimension = 3072)
    {
        _dimension = dimension;
        _vectors = new ConcurrentDictionary<int, float[]>();
        _nextId = 0;
    }

    /// <summary>
    /// Add a vector to the index
    /// </summary>
    public int AddVector(float[] embedding)
    {
        if (embedding.Length != _dimension)
        {
            throw new ArgumentException($"Expected dimension {_dimension}, got {embedding.Length}");
        }

        lock (_lock)
        {
            var id = _nextId++;
            _vectors[id] = embedding;
            return id;
        }
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

        if (_vectors.IsEmpty)
        {
            return (Array.Empty<int>(), Array.Empty<float>());
        }

        // Offload CPU-bound work to thread pool to avoid blocking the async caller
        return await Task.Run(() =>
        {
            var similarities = new List<(int id, float similarity)>();

            foreach (var kvp in _vectors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var similarity = VectorMath.CosineSimilarity(queryEmbedding, kvp.Value);
                similarities.Add((kvp.Key, similarity));
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
    /// Get current vector count
    /// </summary>
    public int Count => _vectors.Count;

    /// <summary>
    /// Clear all vectors
    /// </summary>
    public void Clear()
    {
        _vectors.Clear();
        lock (_lock)
        {
            _nextId = 0;
        }
    }
}
