using System.Text;

namespace DeepResearch.WebApp.Services;

/// <summary>
/// Utility class for splitting large texts into smaller chunks for embedding.
/// Uses a token-aware approach to ensure chunks don't exceed embedding model limits.
/// </summary>
public static class TextChunker
{
    // Very conservative estimate: ~2.5 chars per token to ensure we stay under limits
    // (actual average is ~4, but technical content and special characters can be lower)
    private const int CHARS_PER_TOKEN_ESTIMATE = 2;
    
    /// <summary>
    /// Splits text into chunks that won't exceed the embedding model's token limit.
    /// </summary>
    /// <param name="text">The text to split</param>
    /// <param name="maxTokens">Maximum tokens per chunk (default: 3000 to stay well under 8192 limit)</param>
    /// <param name="overlapTokens">Number of tokens to overlap between chunks (default: 100)</param>
    /// <returns>Array of text chunks</returns>
    public static string[] ChunkText(string text, int maxTokens = 3000, int overlapTokens = 100)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        // Convert token limits to character estimates
        int maxChars = maxTokens * CHARS_PER_TOKEN_ESTIMATE;
        int overlapChars = overlapTokens * CHARS_PER_TOKEN_ESTIMATE;

        // If text is small enough, return as single chunk
        if (text.Length <= maxChars)
        {
            return new[] { text };
        }

        var chunks = new List<string>();
        int startPos = 0;

        while (startPos < text.Length)
        {
            int endPos = Math.Min(startPos + maxChars, text.Length);
            
            // Try to find a good break point (paragraph, sentence, or word boundary)
            if (endPos < text.Length)
            {
                endPos = FindGoodBreakPoint(text, startPos, endPos);
            }

            // Extract the chunk
            string chunk = text.Substring(startPos, endPos - startPos).Trim();
            
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }

            // Move start position for next chunk, with overlap
            if (endPos < text.Length)
            {
                startPos = Math.Max(startPos + 1, endPos - overlapChars);
                
                // Try to start at a word boundary
                while (startPos < text.Length && !char.IsWhiteSpace(text[startPos]) && startPos > endPos - overlapChars)
                {
                    startPos--;
                }
                if (startPos < endPos - overlapChars)
                {
                    startPos = endPos - overlapChars;
                }
            }
            else
            {
                break;
            }
        }

        return chunks.ToArray();
    }

    /// <summary>
    /// Finds a good break point for chunking, preferring paragraph > sentence > word boundaries.
    /// </summary>
    private static int FindGoodBreakPoint(string text, int startPos, int maxEndPos)
    {
        // Look for paragraph break (double newline)
        int searchStart = Math.Max(startPos, maxEndPos - 500);
        int paragraphBreak = text.LastIndexOf("\n\n", maxEndPos, maxEndPos - searchStart);
        if (paragraphBreak > searchStart)
        {
            return paragraphBreak + 2;
        }

        // Look for sentence ending
        searchStart = Math.Max(startPos, maxEndPos - 300);
        for (int i = maxEndPos - 1; i > searchStart; i--)
        {
            if (IsSentenceEnding(text, i))
            {
                return i + 1;
            }
        }

        // Look for word boundary
        searchStart = Math.Max(startPos, maxEndPos - 100);
        for (int i = maxEndPos - 1; i > searchStart; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i + 1;
            }
        }

        // No good break point found, just break at max
        return maxEndPos;
    }

    /// <summary>
    /// Checks if position is at a sentence ending (., !, ?) followed by space or newline.
    /// </summary>
    private static bool IsSentenceEnding(string text, int pos)
    {
        if (pos >= text.Length - 1)
        {
            return false;
        }

        char current = text[pos];
        char next = text[pos + 1];

        return (current == '.' || current == '!' || current == '?') && 
               (char.IsWhiteSpace(next) || next == '\n');
    }

    /// <summary>
    /// Creates a summary identifier for a chunk (first 50 chars).
    /// </summary>
    public static string GetChunkIdentifier(string chunk, int chunkIndex, int totalChunks)
    {
        string preview = chunk.Length > 50 
            ? chunk.Substring(0, 50).Trim() + "..." 
            : chunk.Trim();
        
        return $"[Chunk {chunkIndex + 1}/{totalChunks}] {preview}";
    }
}
