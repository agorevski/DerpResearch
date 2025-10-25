using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using System.Text.Json;
using System.Runtime.CompilerServices;

namespace DeepResearch.WebApp.Services;

/// <summary>
/// Mock implementation of ILLMService for testing UX without Azure OpenAI
/// </summary>
public class MockLLMService : ILLMService
{
    private readonly ILogger<MockLLMService> _logger;
    private static readonly Random _random = new();

    public MockLLMService(ILogger<MockLLMService> logger)
    {
        _logger = logger;
        _logger.LogInformation("MockLLMService initialized - responses will be simulated");
    }

    public async IAsyncEnumerable<string> ChatCompletionStream(
        ChatMessage[] messages,
        string deploymentName = "gpt-4o")
    {
        _logger.LogInformation("Mock streaming chat completion for {DeploymentName}", deploymentName);

        var lastMessage = messages.LastOrDefault()?.Content ?? "";
        var response = GenerateMockResponse(lastMessage, deploymentName);

        // Stream word by word with realistic delays
        var words = response.Split(' ');
        foreach (var word in words)
        {
            await Task.Delay(_random.Next(20, 80));
            yield return word + " ";
        }
    }

    public async Task<string> ChatCompletion(
        ChatMessage[] messages,
        string deploymentName = "gpt-4o")
    {
        _logger.LogInformation("Mock non-streaming chat completion for {DeploymentName}", deploymentName);

        var lastMessage = messages.LastOrDefault()?.Content ?? "";
        var response = GenerateMockResponse(lastMessage, deploymentName);

        // Simulate API latency
        await Task.Delay(_random.Next(100, 300));

        return response;
    }

    public async Task<float[]> GetEmbedding(string text)
    {
        _logger.LogInformation("Mock embedding generation for text length: {Length}", text.Length);

        // Simulate API latency
        await Task.Delay(_random.Next(50, 150));

        // Generate deterministic random embedding based on text hash
        var seed = text.GetHashCode();
        var random = new Random(seed);
        var embedding = new float[3072]; // text-embedding-3-large dimensions

        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // Range: -1 to 1
        }

        // Normalize the vector
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] /= (float)magnitude;
        }

        return embedding;
    }

    public async Task<T?> GetStructuredOutput<T>(
        string prompt,
        string deploymentName = "gpt-4o") where T : class
    {
        _logger.LogInformation("Mock structured output for type: {Type}", typeof(T).Name);

        // Simulate API latency
        await Task.Delay(_random.Next(200, 500));

        // Generate mock structured responses based on type
        if (typeof(T) == typeof(ResearchPlan))
        {
            return GenerateMockResearchPlan(prompt) as T;
        }
        else if (typeof(T) == typeof(ReflectionResult))
        {
            return GenerateMockReflectionResult() as T;
        }
        else if (typeof(T) == typeof(ClarificationResult))
        {
            return GenerateMockClarificationResult(prompt) as T;
        }

        _logger.LogWarning("No mock implementation for type {Type}", typeof(T).Name);
        return null;
    }

    private string GenerateMockResponse(string userMessage, string deploymentName)
    {
        // Generate contextual response based on keywords
        var lowerMessage = userMessage.ToLower();

        if (lowerMessage.Contains("compare") || lowerMessage.Contains("difference"))
        {
            return "Based on the research, here's a comprehensive comparison:\n\n" +
                   "**Key Differences:**\n" +
                   "1. Architecture varies significantly between the two approaches [1]\n" +
                   "2. Performance metrics show distinct advantages in different scenarios [2]\n" +
                   "3. Use cases differ based on scalability requirements [3]\n\n" +
                   "**Similarities:**\n" +
                   "Both share common foundational principles and design patterns [4]. " +
                   "The implementation details reveal interesting trade-offs [5].";
        }
        else if (lowerMessage.Contains("how") || lowerMessage.Contains("explain"))
        {
            return "Let me explain this concept in detail:\n\n" +
                   "The fundamental mechanism works by combining multiple components [1]. " +
                   "Research shows that this approach offers several advantages [2]:\n\n" +
                   "1. **Efficiency**: Optimized processing reduces overhead [3]\n" +
                   "2. **Scalability**: Handles increasing loads effectively [4]\n" +
                   "3. **Reliability**: Built-in redundancy ensures stability [5]\n\n" +
                   "Recent studies demonstrate promising results across various benchmarks [6].";
        }
        else if (lowerMessage.Contains("what") || lowerMessage.Contains("define"))
        {
            return "Based on current research and documentation:\n\n" +
                   "This concept represents a sophisticated approach to solving complex problems [1]. " +
                   "Industry experts define it as a comprehensive framework that integrates " +
                   "multiple technologies and methodologies [2].\n\n" +
                   "**Core Components:**\n" +
                   "- Foundation layer handling basic operations [3]\n" +
                   "- Processing tier managing complex workflows [4]\n" +
                   "- Interface layer ensuring seamless interaction [5]\n\n" +
                   "The implementation varies depending on specific requirements and constraints [6].";
        }
        else
        {
            return "Thank you for your question. Based on the available information:\n\n" +
                   "Research indicates several important findings [1]. " +
                   "The data suggests that multiple factors contribute to the overall outcome [2]. " +
                   "Analysis reveals interesting patterns across different scenarios [3].\n\n" +
                   "Key considerations include:\n" +
                   "- Technical feasibility and resource requirements [4]\n" +
                   "- Performance implications and optimization strategies [5]\n" +
                   "- Long-term maintenance and scalability concerns [6]\n\n" +
                   "Further investigation may be needed for specific edge cases [7].";
        }
    }

    private ResearchPlan GenerateMockResearchPlan(string query)
    {
        var tasks = new[]
        {
            new ResearchTask
            {
                Description = "Find foundational information and definitions",
                SearchQuery = $"{query} definition overview",
                Priority = 1
            },
            new ResearchTask
            {
                Description = "Investigate technical details and specifications",
                SearchQuery = $"{query} technical details architecture",
                Priority = 2
            },
            new ResearchTask
            {
                Description = "Research practical applications and use cases",
                SearchQuery = $"{query} applications use cases examples",
                Priority = 3
            },
            new ResearchTask
            {
                Description = "Compare with alternatives and best practices",
                SearchQuery = $"{query} comparison alternatives best practices",
                Priority = 4
            }
        };

        return new ResearchPlan
        {
            MainGoal = $"Comprehensively research: {query}",
            Subtasks = tasks.Take(_random.Next(2, 5)).ToArray(),
            KeyConcepts = new[] { "foundations", "implementation", "best practices", "trade-offs" }
        };
    }

    private ReflectionResult GenerateMockReflectionResult()
    {
        return new ReflectionResult
        {
            ConfidenceScore = 0.95f,
            IdentifiedGaps = new[] { "Looks good, thank you!", "Could be slightly more details" },
            SuggestedAdditionalSearches = new[] { "latest advancements", "case studies" },
            RequiresMoreResearch = false
        };
        // // Vary confidence to test iteration logic
        // var confidence = _random.NextDouble();

        // return new ReflectionResult
        // {
        //     ConfidenceScore = confidence,
        //     IdentifiedGaps = confidence < 0.7
        //         ? new[] { "Need more recent data", "Missing comparative analysis" }
        //         : Array.Empty<string>(),
        //     SuggestedAdditionalSearches = confidence < 0.7
        //         ? new[] { "latest research papers", "industry benchmarks" }
        //         : Array.Empty<string>(),
        //     RequiresMoreResearch = confidence < 0.7
        // };
    }

    private ClarificationResult GenerateMockClarificationResult(string query)
    {
        var questions = new List<string>
        {
            $"Are you looking for technical details or a high-level overview of '{query}'?",
            "Would you like information about recent developments or historical context?",
            "Should the research focus on practical applications or theoretical foundations?"
        };

        return new ClarificationResult
        {
            Questions = questions.Take(_random.Next(2, 4)).ToArray(),
            Rationale = "To provide the most relevant and comprehensive research, " +
                       "I need to understand your specific focus and depth requirements."
        };
    }
}
