using DeepResearch.WebApp.Services;
using FluentAssertions;

namespace DerpResearch.Tests.Unit.Services;

public class TextChunkerTests
{
    [Fact]
    public void ChunkText_ShouldReturnSingleChunk_WhenTextIsShorterThanMaxTokens()
    {
        // Arrange
        var shortText = "This is a short text that fits in one chunk.";
        
        // Act
        var chunks = TextChunker.ChunkText(shortText, maxTokens: 1000, overlapTokens: 100);
        
        // Assert
        chunks.Should().HaveCount(1);
        chunks[0].Should().Be(shortText);
    }

    [Fact]
    public void ChunkText_ShouldCreateMultipleChunks_WhenTextExceedsMaxTokens()
    {
        // Arrange
        var longText = string.Join(" ", Enumerable.Repeat("word", 5000));
        
        // Act
        var chunks = TextChunker.ChunkText(longText, maxTokens: 1000, overlapTokens: 100);
        
        // Assert
        chunks.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void ChunkText_ShouldHaveOverlap_BetweenConsecutiveChunks()
    {
        // Arrange
        var text = string.Join(" ", Enumerable.Repeat("test", 2000));
        var overlapTokens = 50;
        
        // Act
        var chunks = TextChunker.ChunkText(text, maxTokens: 500, overlapTokens: overlapTokens);
        
        // Assert
        chunks.Should().HaveCountGreaterThan(1);
        
        // Check that consecutive chunks have some overlapping content
        var chunkCount = chunks.Length;
        for (int i = 0; i < chunkCount - 1; i++)
        {
            var currentChunkEnd = chunks[i].Substring(Math.Max(0, chunks[i].Length - 200));
            var nextChunkStart = chunks[i + 1].Substring(0, Math.Min(200, chunks[i + 1].Length));
            
            // There should be some common words between end of current and start of next
            var currentWords = currentChunkEnd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var nextWords = nextChunkStart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            currentWords.Intersect(nextWords).Should().NotBeEmpty(
                "because chunks should have overlapping content");
        }
    }

    [Fact]
    public void ChunkText_ShouldHandleEmptyString()
    {
        // Arrange
        var emptyText = string.Empty;
        
        // Act
        var chunks = TextChunker.ChunkText(emptyText, maxTokens: 1000, overlapTokens: 100);
        
        // Assert
        chunks.Should().BeEmpty();
    }

    [Fact]
    public void ChunkText_ShouldHandleNullString()
    {
        // Arrange
        string? nullText = null;
        
        // Act
        var chunks = TextChunker.ChunkText(nullText!, maxTokens: 1000, overlapTokens: 100);
        
        // Assert
        chunks.Should().BeEmpty();
    }

    [Fact]
    public void ChunkText_ShouldRespectMaxTokenLimit()
    {
        // Arrange
        var text = string.Join(" ", Enumerable.Repeat("word", 3000));
        var maxTokens = 1000;
        
        // Act
        var chunks = TextChunker.ChunkText(text, maxTokens: maxTokens, overlapTokens: 100);
        
        // Assert
        foreach (var chunk in chunks)
        {
            // Approximate token count (1 token ≈ 4 characters)
            var approximateTokens = chunk.Length / 4;
            approximateTokens.Should().BeLessOrEqualTo((int)(maxTokens * 1.5), // Allow some margin
                "because chunks should not significantly exceed max tokens");
        }
    }

    [Fact]
    public void ChunkText_ShouldPreserveSentenceBoundaries_WhenPossible()
    {
        // Arrange
        var text = string.Join(". ", Enumerable.Repeat("This is a sentence", 500)) + ".";
        
        // Act
        var chunks = TextChunker.ChunkText(text, maxTokens: 1000, overlapTokens: 100);
        
        // Assert
        chunks.Should().HaveCountGreaterThan(1);
        
        // Check that most chunks end with sentence-ending punctuation
        var chunksEndingWithPeriod = chunks.Count(c => c.TrimEnd().EndsWith('.'));
        var totalChunks = chunks.Length;
        var percentageWithPeriod = (double)chunksEndingWithPeriod / (double)totalChunks;
        
        percentageWithPeriod.Should().BeGreaterThan(0.5, 
            "because chunker should try to preserve sentence boundaries");
    }

    [Fact]
    public void ChunkText_ShouldHandleTextWithNewlines()
    {
        // Arrange
        var text = "Line 1\nLine 2\nLine 3\n" + string.Join("\n", Enumerable.Repeat("Content line", 500));
        
        // Act
        var chunks = TextChunker.ChunkText(text, maxTokens: 1000, overlapTokens: 100);
        
        // Assert
        chunks.Should().NotBeEmpty();
        chunks.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void ChunkText_WithZeroOverlap_ShouldNotHaveOverlappingContent()
    {
        // Arrange
        var text = string.Join(" ", Enumerable.Range(1, 2000).Select(i => $"word{i}"));
        
        // Act
        var chunks = TextChunker.ChunkText(text, maxTokens: 500, overlapTokens: 0);
        
        // Assert
        chunks.Should().HaveCountGreaterThan(1);
        
        // Verify no overlap by checking that each word appears only once across all chunks
        var allWords = new HashSet<string>();
        foreach (var chunk in chunks)
        {
            var words = chunk.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                allWords.Add(word);
            }
        }
        
        // Count total words across all chunks
        var totalWordsInChunks = chunks.Sum(c => 
            c.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        
        // With zero overlap, unique words should equal total words (approximately)
        var ratio = (double)allWords.Count / totalWordsInChunks;
        ratio.Should().BeGreaterThan(0.9, 
            "because with zero overlap, most words should appear only once");
    }

    [Fact]
    public void ChunkText_ShouldHandleVerySmallMaxTokens()
    {
        // Arrange
        var text = "This is a test sentence with several words in it.";
        
        // Act
        var chunks = TextChunker.ChunkText(text, maxTokens: 5, overlapTokens: 1);
        
        // Assert
        chunks.Should().NotBeEmpty();
        chunks.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void ChunkText_ShouldHandleUnicodeCharacters()
    {
        // Arrange
        var text = "Hello 世界! " + string.Join(" ", Enumerable.Repeat("Emoji 😀 test", 500));
        
        // Act
        var chunks = TextChunker.ChunkText(text, maxTokens: 1000, overlapTokens: 100);
        
        // Assert
        chunks.Should().NotBeEmpty();
        chunks[0].Should().Contain("世界");
        chunks.Should().Contain(c => c.Contains("😀"));
    }

    [Theory]
    [InlineData(100, 10)]
    [InlineData(500, 50)]
    [InlineData(1000, 100)]
    [InlineData(3000, 300)]
    public void ChunkText_ShouldWorkWithVariousParameters(int maxTokens, int overlap)
    {
        // Arrange
        var text = string.Join(" ", Enumerable.Repeat("test word", 2000));
        
        // Act
        var chunks = TextChunker.ChunkText(text, maxTokens: maxTokens, overlapTokens: overlap);
        
        // Assert
        chunks.Should().NotBeEmpty();
        
        // Verify overlap is less than maxTokens
        overlap.Should().BeLessThan(maxTokens);
    }
}
