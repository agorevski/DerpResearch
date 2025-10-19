using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using System.Runtime.CompilerServices;

namespace DeepResearch.WebApp.Agents;

/// <summary>
/// Mock implementation of IClarificationAgent for testing
/// </summary>
public class MockClarificationAgent : IClarificationAgent
{
    private readonly ILogger<MockClarificationAgent> _logger;
    private static readonly Random _random = new();

    public MockClarificationAgent(ILogger<MockClarificationAgent> logger)
    {
        _logger = logger;
        _logger.LogInformation("MockClarificationAgent initialized");
    }

    public async Task<ClarificationResult> GenerateClarifyingQuestionsAsync(
        string userQuery,
        ConversationContext context,
        int derpificationLevel = 100)
    {
        _logger.LogInformation("Generating mock clarifying questions for: {Query}", userQuery);
        await Task.Delay(_random.Next(200, 400));

        var questions = new List<string>
        {
            $"What specific aspect of '{userQuery}' are you most interested in?",
            "Are you looking for technical details, practical applications, or theoretical background?",
            "Would you like information about recent developments or historical context?",
            "Should the research focus on a particular industry or use case?"
        };

        // Higher derpification = more questions
        var numQuestions = derpificationLevel switch
        {
            >= 70 => _random.Next(3, 5),
            >= 40 => _random.Next(2, 3),
            _ => _random.Next(1, 2)
        };

        return new ClarificationResult
        {
            Questions = questions.Take(numQuestions).ToArray(),
            Rationale = "These questions will help me provide more focused and relevant research results."
        };
    }
}

/// <summary>
/// Mock implementation of IPlannerAgent for testing
/// </summary>
public class MockPlannerAgent : IPlannerAgent
{
    private readonly ILogger<MockPlannerAgent> _logger;
    private static readonly Random _random = new();

    public MockPlannerAgent(ILogger<MockPlannerAgent> logger)
    {
        _logger = logger;
        _logger.LogInformation("MockPlannerAgent initialized");
    }

    public async Task<ResearchPlan> CreatePlanAsync(
        string userQuery,
        ConversationContext context,
        int derpificationLevel = 100)
    {
        _logger.LogInformation("Creating mock research plan for: {Query} (derpification: {Level})",
            userQuery, derpificationLevel);

        await Task.Delay(_random.Next(300, 600));

        // Generate subtasks based on derpification level
        var allTasks = new[]
        {
            new ResearchTask
            {
                Description = "Research foundational concepts and definitions",
                SearchQuery = $"{userQuery} definition fundamentals basics",
                Priority = 1
            },
            new ResearchTask
            {
                Description = "Investigate technical architecture and implementation",
                SearchQuery = $"{userQuery} architecture technical implementation",
                Priority = 2
            },
            new ResearchTask
            {
                Description = "Explore practical applications and use cases",
                SearchQuery = $"{userQuery} applications use cases examples",
                Priority = 3
            },
            new ResearchTask
            {
                Description = "Compare with alternatives and analyze trade-offs",
                SearchQuery = $"{userQuery} comparison alternatives pros cons",
                Priority = 4
            },
            new ResearchTask
            {
                Description = "Review current trends and future directions",
                SearchQuery = $"{userQuery} trends future outlook developments",
                Priority = 5
            }
        };

        // More derpification = more subtasks
        var numTasks = derpificationLevel switch
        {
            >= 70 => _random.Next(4, 6),
            >= 40 => _random.Next(2, 4),
            _ => _random.Next(1, 2)
        };

        return new ResearchPlan
        {
            MainGoal = $"Comprehensively research: {userQuery}",
            Subtasks = allTasks.Take(numTasks).ToArray(),
            KeyConcepts = new[] { "foundations", "implementation", "applications", "comparisons", "trends" }
        };
    }
}

/// <summary>
/// Mock implementation of ISearchAgent for testing
/// </summary>
public class MockSearchAgent : ISearchAgent
{
    private readonly ILogger<MockSearchAgent> _logger;
    private readonly ISearchService _searchService;
    private readonly IWebContentFetcher _webContentFetcher;
    private readonly IMemoryService _memoryService;
    private static readonly Random _random = new();

    public MockSearchAgent(
        ILogger<MockSearchAgent> logger,
        ISearchService searchService,
        IWebContentFetcher webContentFetcher,
        IMemoryService memoryService)
    {
        _logger = logger;
        _searchService = searchService;
        _webContentFetcher = webContentFetcher;
        _memoryService = memoryService;
        _logger.LogInformation("MockSearchAgent initialized");
    }

    public async IAsyncEnumerable<object> ExecuteSearchPlanAsync(
        ResearchPlan plan,
        int derpificationLevel = 100)
    {
        _logger.LogInformation("Executing mock search plan with {TaskCount} tasks", plan.Subtasks.Length);

        var allResults = new List<SearchResult>();
        var allStoredMemoryIds = new List<string>();

        foreach (var task in plan.Subtasks)
        {
            await Task.Delay(_random.Next(200, 400));

            // Perform search
            var maxResults = derpificationLevel switch
            {
                >= 70 => 10,
                >= 40 => 5,
                _ => 3
            };

            var searchResults = await _searchService.SearchAsync(task.SearchQuery, maxResults);
            _logger.LogInformation("Mock search returned {Count} results for: {Query}",
                searchResults.Length, task.SearchQuery);

            // Fetch content for results
            var urls = searchResults.Select(r => r.Url).ToArray();
            if (urls.Length > 0)
            {
                var contentMap = await _webContentFetcher.FetchContentAsync(urls);

                // Update results with fetched content
                foreach (var result in searchResults)
                {
                    if (contentMap.TryGetValue(result.Url, out var content))
                    {
                        result.Content = content;
                    }
                }
            }

            // Store in memory and yield each result immediately (like real SearchAgent)
            foreach (var result in searchResults.Where(r => !string.IsNullOrEmpty(r.Content)))
            {
                var memoryId = await _memoryService.StoreMemoryAsync(
                    result.Content!,
                    source: result.Url,
                    tags: new[] { "web-search", task.SearchQuery }
                );
                allStoredMemoryIds.Add(memoryId);
                allResults.Add(result);
                
                // Yield SearchResult (OrchestratorService will convert to SSE source update)
                yield return result;
                
                await Task.Delay(_random.Next(100, 200));
            }
        }

        // Emit final gathered information
        yield return new GatheredInformation
        {
            Results = allResults.ToArray(),
            StoredMemoryIds = allStoredMemoryIds.ToArray(),
            TotalSourcesFound = allResults.Count
        };
    }
}

/// <summary>
/// Mock implementation of ISynthesisAgent for testing
/// </summary>
public class MockSynthesisAgent : ISynthesisAgent
{
    private readonly ILogger<MockSynthesisAgent> _logger;
    private static readonly Random _random = new();

    public MockSynthesisAgent(ILogger<MockSynthesisAgent> logger)
    {
        _logger = logger;
        _logger.LogInformation("MockSynthesisAgent initialized");
    }

    public async IAsyncEnumerable<string> SynthesizeAsync(
        string userQuery,
        ResearchPlan plan,
        GatheredInformation info,
        MemoryChunk[] relevantMemories,
        int derpificationLevel = 100)
    {
        _logger.LogInformation("Synthesizing mock response for: {Query} with {SourceCount} sources",
            userQuery, info.TotalSourcesFound);

        // Generate comprehensive response based on derpification level
        var response = GenerateSynthesizedResponse(userQuery, info.TotalSourcesFound, derpificationLevel);

        // Stream word by word
        var words = response.Split(' ');
        foreach (var word in words)
        {
            await Task.Delay(_random.Next(30, 100));
            yield return word + " ";
        }
    }

    private string GenerateSynthesizedResponse(string query, int sourceCount, int derpificationLevel)
    {
        var intro = $"Based on comprehensive research across {sourceCount} sources, here's what I found about {query}:\n\n";

        var mainContent = derpificationLevel switch
        {
            >= 70 => GenerateDetailedResponse(),
            >= 40 => GenerateModerateResponse(),
            _ => GenerateConciseResponse()
        };

        return intro + mainContent;
    }

    private string GenerateDetailedResponse()
    {
        return "**Overview**\n" +
               "The research reveals a sophisticated and multifaceted topic with significant implications [1]. " +
               "Current literature emphasizes the importance of understanding both foundational principles and " +
               "emerging trends [2].\n\n" +
               "**Technical Architecture**\n" +
               "The underlying architecture demonstrates several key design patterns [3]:\n" +
               "- Modular component organization enabling scalability [4]\n" +
               "- Event-driven communication reducing coupling [5]\n" +
               "- Distributed processing for improved throughput [6]\n\n" +
               "**Practical Applications**\n" +
               "Industry adoption shows promising results across multiple sectors [7]. " +
               "Case studies indicate significant performance improvements when properly implemented [8]. " +
               "Organizations report reduced operational costs and enhanced capabilities [9].\n\n" +
               "**Comparative Analysis**\n" +
               "Compared to alternative approaches, this solution offers distinct advantages in flexibility " +
               "and maintainability [10]. However, trade-offs exist in terms of initial complexity and " +
               "learning curve [11]. The choice depends on specific requirements and constraints [12].\n\n" +
               "**Future Outlook**\n" +
               "Emerging trends suggest continued evolution and refinement [13]. Research indicates potential " +
               "breakthroughs in efficiency and scalability [14]. Industry experts predict widespread adoption " +
               "within the next few years [15].";
    }

    private string GenerateModerateResponse()
    {
        return "**Key Findings**\n" +
               "Research indicates this is a well-established approach with proven benefits [1]. " +
               "The core concepts are supported by extensive documentation and case studies [2].\n\n" +
               "**Implementation Considerations**\n" +
               "Successful deployment requires careful planning and resource allocation [3]. " +
               "Organizations typically see results within several months of adoption [4]. " +
               "Best practices emphasize iterative implementation and continuous improvement [5].\n\n" +
               "**Practical Benefits**\n" +
               "Users report improved efficiency and better outcomes [6]. " +
               "The approach scales well for growing organizations [7]. " +
               "Integration with existing systems is generally straightforward [8].";
    }

    private string GenerateConciseResponse()
    {
        return "**Summary**\n" +
               "This represents a proven approach with strong industry support [1]. " +
               "The fundamentals are well-documented and widely understood [2].\n\n" +
               "**Key Points**\n" +
               "- Effective for solving common challenges [3]\n" +
               "- Scales appropriately with organizational needs [4]\n" +
               "- Supported by robust ecosystem and tools [5]";
    }
}

/// <summary>
/// Mock implementation of IReflectionAgent for testing
/// </summary>
public class MockReflectionAgent : IReflectionAgent
{
    private readonly ILogger<MockReflectionAgent> _logger;
    private readonly IConfiguration _configuration;
    private static readonly Random _random = new();

    public MockReflectionAgent(ILogger<MockReflectionAgent> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _logger.LogInformation("MockReflectionAgent initialized");
    }

    public async Task<ReflectionResult> ReflectAsync(
        string userQuery,
        string synthesizedResponse,
        GatheredInformation info,
        int derpificationLevel = 100)
    {
        _logger.LogInformation("Reflecting on response quality for: {Query}", userQuery);
        await Task.Delay(_random.Next(300, 600));

        // Check if we should use fixed confidence (for testing)
        var useFixedConfidence = _configuration.GetValue<bool>("MockServices:UseFixedConfidence", false);
        var fixedConfidenceScore = _configuration.GetValue<float>("MockServices:FixedConfidenceScore", 0.95f);

        float confidence;
        if (useFixedConfidence)
        {
            confidence = fixedConfidenceScore;
            _logger.LogInformation("Using FIXED confidence score: {Confidence}", confidence);
        }
        else
        {
            // Vary confidence to test iteration logic
            // Higher derpification = higher standards = sometimes lower confidence
            var baseConfidence = _random.NextDouble() * 0.3 + 0.5; // 0.5 to 0.8
            var derpAdjustment = derpificationLevel > 70 ? -0.15 : 0;
            confidence = (float)Math.Max(0.4, Math.Min(1.0, baseConfidence + derpAdjustment));
            _logger.LogInformation("Using RANDOM confidence score: {Confidence}", confidence);
        }

        var needsMoreResearch = confidence < 0.7;

        return new ReflectionResult
        {
            ConfidenceScore = confidence,
            IdentifiedGaps = needsMoreResearch
                ? new[]
                {
                    "Could benefit from more recent data and statistics",
                    "Additional comparative analysis would strengthen conclusions",
                    "More specific examples would improve practical relevance"
                }.Take(_random.Next(1, 3)).ToArray()
                : Array.Empty<string>(),
            SuggestedAdditionalSearches = needsMoreResearch
                ? new[]
                {
                    $"{userQuery} recent developments 2024",
                    $"{userQuery} case studies examples",
                    $"{userQuery} benchmarks performance metrics"
                }.Take(_random.Next(1, 3)).ToArray()
                : Array.Empty<string>(),
            RequiresMoreResearch = needsMoreResearch
        };
    }
}
