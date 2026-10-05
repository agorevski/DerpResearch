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
}
