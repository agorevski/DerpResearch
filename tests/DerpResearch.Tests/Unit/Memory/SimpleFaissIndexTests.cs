using DeepResearch.WebApp.Memory;
using Xunit;

namespace DerpResearch.Tests.Unit.Memory;

public class SimpleFaissIndexTests
{
    [Fact]
    public void AddVector_ShouldReturnSequentialIds()
    {
        // Arrange
        var index = new SimpleFaissIndex(dimension: 3);
        var embedding1 = new float[] { 1.0f, 2.0f, 3.0f };
        var embedding2 = new float[] { 4.0f, 5.0f, 6.0f };

        // Act
        var id1 = index.AddVector(embedding1);
        var id2 = index.AddVector(embedding2);

        // Assert
        Assert.Equal(0, id1);
        Assert.Equal(1, id2);
        Assert.Equal(2, index.Count);
    }

    [Fact]
    public void AddVector_WithIncorrectDimension_ShouldThrowException()
    {
        // Arrange
        var index = new SimpleFaissIndex(dimension: 3);
        var wrongEmbedding = new float[] { 1.0f, 2.0f }; // Only 2 dimensions

        // Act & Assert
        Assert.Throws<ArgumentException>(() => index.AddVector(wrongEmbedding));
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnMostSimilarVectors()
    {
        // Arrange
        var index = new SimpleFaissIndex(dimension: 3);

        // Add vectors
        var vector1 = new float[] { 1.0f, 0.0f, 0.0f };
        var vector2 = new float[] { 0.0f, 1.0f, 0.0f };
        var vector3 = new float[] { 0.9f, 0.1f, 0.0f }; // Very similar to vector1

        index.AddVector(vector1);
        index.AddVector(vector2);
        index.AddVector(vector3);

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
    public async Task SearchAsync_WithEmptyIndex_ShouldReturnEmpty()
    {
        // Arrange
        var index = new SimpleFaissIndex(dimension: 3);
        var query = new float[] { 1.0f, 2.0f, 3.0f };

        // Act
        var (ids, distances) = await index.SearchAsync(query, topK: 5);

        // Assert
        Assert.Empty(ids);
        Assert.Empty(distances);
    }

    [Fact]
    public async Task SearchAsync_WithTopKLargerThanCount_ShouldReturnAllVectors()
    {
        // Arrange
        var index = new SimpleFaissIndex(dimension: 3);

        index.AddVector(new float[] { 1.0f, 0.0f, 0.0f });
        index.AddVector(new float[] { 0.0f, 1.0f, 0.0f });

        // Act - Request more vectors than available
        var query = new float[] { 1.0f, 0.0f, 0.0f };
        var (ids, distances) = await index.SearchAsync(query, topK: 10);

        // Assert
        Assert.Equal(2, ids.Length); // Should only return 2 vectors
        Assert.Equal(2, distances.Length);
    }

    [Fact]
    public async Task SearchAsync_WithIncorrectDimension_ShouldThrowException()
    {
        // Arrange
        var index = new SimpleFaissIndex(dimension: 3);
        index.AddVector(new float[] { 1.0f, 0.0f, 0.0f });

        var wrongQuery = new float[] { 1.0f, 0.0f }; // Only 2 dimensions

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await index.SearchAsync(wrongQuery, topK: 1));
    }

    [Fact]
    public async Task CosineSimilarity_ShouldComputeCorrectly()
    {
        // Arrange
        var index = new SimpleFaissIndex(dimension: 3);

        // Two identical vectors should have similarity of 1.0
        var vector1 = new float[] { 1.0f, 0.0f, 0.0f };
        index.AddVector(vector1);

        // Act
        var (ids, distances) = await index.SearchAsync(vector1, topK: 1);

        // Assert
        Assert.Single(ids);
        Assert.InRange(distances[0], 0.999f, 1.001f); // Should be very close to 1.0
    }

    [Fact]
    public void Clear_ShouldRemoveAllVectors()
    {
        // Arrange
        var index = new SimpleFaissIndex(dimension: 3);
        index.AddVector(new float[] { 1.0f, 0.0f, 0.0f });
        index.AddVector(new float[] { 0.0f, 1.0f, 0.0f });
        Assert.Equal(2, index.Count);

        // Act
        index.Clear();

        // Assert
        Assert.Equal(0, index.Count);
    }

    [Fact]
    public async Task SearchAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var index = new SimpleFaissIndex(dimension: 100);

        // Add many vectors to make search take longer
        for (int i = 0; i < 1000; i++)
        {
            var embedding = new float[100];
            for (int j = 0; j < 100; j++)
            {
                embedding[j] = (float)(i + j * 0.1);
            }
            index.AddVector(embedding);
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
        var index = new SimpleFaissIndex(dimension: 3);

        // Add test vectors
        index.AddVector(new float[] { 1.0f, 0.0f, 0.0f });
        index.AddVector(new float[] { 0.0f, 1.0f, 0.0f });
        index.AddVector(new float[] { 0.0f, 0.0f, 1.0f });

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
    public async Task SearchAsync_WithOrthogonalVectors_ShouldReturnZeroSimilarity()
    {
        // Arrange
        var index = new SimpleFaissIndex(dimension: 3);

        // Add orthogonal vectors (perpendicular in 3D space)
        var vector1 = new float[] { 1.0f, 0.0f, 0.0f };
        var vector2 = new float[] { 0.0f, 1.0f, 0.0f };

        index.AddVector(vector1);
        index.AddVector(vector2);

        // Act - Search with first vector
        var (ids, distances) = await index.SearchAsync(vector1, topK: 2);

        // Assert
        Assert.Equal(2, ids.Length);
        Assert.InRange(distances[0], 0.999f, 1.001f); // Self-similarity should be ~1.0
        Assert.InRange(distances[1], -0.001f, 0.001f); // Orthogonal similarity should be ~0.0
    }

    [Fact]
    public void Count_ShouldReflectNumberOfVectors()
    {
        // Arrange
        var index = new SimpleFaissIndex(dimension: 3);

        // Act & Assert
        Assert.Equal(0, index.Count);

        index.AddVector(new float[] { 1.0f, 0.0f, 0.0f });
        Assert.Equal(1, index.Count);

        index.AddVector(new float[] { 0.0f, 1.0f, 0.0f });
        Assert.Equal(2, index.Count);

        index.Clear();
        Assert.Equal(0, index.Count);
    }

    [Fact]
    public async Task SearchAsync_AfterClear_ShouldReturnEmpty()
    {
        // Arrange
        var index = new SimpleFaissIndex(dimension: 3);
        index.AddVector(new float[] { 1.0f, 0.0f, 0.0f });
        index.AddVector(new float[] { 0.0f, 1.0f, 0.0f });

        // Act
        index.Clear();
        var (ids, distances) = await index.SearchAsync(new float[] { 1.0f, 0.0f, 0.0f }, topK: 5);

        // Assert
        Assert.Empty(ids);
        Assert.Empty(distances);
    }

    [Fact]
    public async Task SearchAsync_WithNormalizedVectors_ShouldWorkCorrectly()
    {
        // Arrange
        var index = new SimpleFaissIndex(dimension: 3);

        // Add normalized vectors (unit length)
        var vector1 = new float[] { 0.577f, 0.577f, 0.577f }; // Normalized [1,1,1]
        var vector2 = new float[] { 1.0f, 0.0f, 0.0f };
        var vector3 = new float[] { 0.0f, 1.0f, 0.0f };

        index.AddVector(vector1);
        index.AddVector(vector2);
        index.AddVector(vector3);

        // Act
        var query = new float[] { 0.577f, 0.577f, 0.577f };
        var (ids, distances) = await index.SearchAsync(query, topK: 3);

        // Assert
        Assert.Equal(3, ids.Length);
        Assert.Equal(0, ids[0]); // Should find itself first
        Assert.InRange(distances[0], 0.999f, 1.001f);
    }
}
