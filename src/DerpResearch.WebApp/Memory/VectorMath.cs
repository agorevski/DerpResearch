namespace DeepResearch.WebApp.Memory;

internal static class VectorMath
{
    /// <summary>
    /// Calculate cosine similarity between two vectors
    /// </summary>
    public static float CosineSimilarity(float[] a, float[] b)
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
    /// Rank vectors by cosine similarity to the query and return the top-K ids and scores.
    /// </summary>
    public static (int[] ids, float[] distances) RankByCosineSimilarity(
        float[] query,
        IEnumerable<KeyValuePair<int, float[]>> vectors,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var similarities = new List<(int id, float similarity)>();
        foreach (var kvp in vectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            similarities.Add((kvp.Key, CosineSimilarity(query, kvp.Value)));
        }

        var topResults = similarities
            .OrderByDescending(x => x.similarity)
            .Take(topK)
            .ToArray();

        return (
            topResults.Select(x => x.id).ToArray(),
            topResults.Select(x => x.similarity).ToArray());
    }
}
