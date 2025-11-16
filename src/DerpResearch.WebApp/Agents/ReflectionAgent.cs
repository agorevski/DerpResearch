using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using System.Text.Json;

namespace DeepResearch.WebApp.Agents;

public class ReflectionAgent : IReflectionAgent
{
    private readonly ILLMService _llmService;
    private readonly ILogger<ReflectionAgent> _logger;

    public ReflectionAgent(ILLMService llmService, ILogger<ReflectionAgent> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<ReflectionResult> ReflectAsync(
        string userQuery,
        string synthesizedResponse,
        GatheredInformation info,
        int derpificationLevel = 100,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var evaluationGuidance = GetEvaluationGuidance(derpificationLevel);
        
        var prompt = $@"Evaluate the quality and completeness of this research response.

{evaluationGuidance}

Original Question: ""{userQuery}""

Generated Response:
{synthesizedResponse}

Sources Used: {info.TotalSourcesFound}

Evaluate based on:
1. Number and quality of citations
2. Coverage of the original question
3. Factual density (specific facts vs. generalizations)
4. Identified knowledge gaps or limitations

Return a JSON object with:
- confidenceScore: 0.0 to 1.0 (0.7+ is good, below 0.5 needs more research)
- identifiedGaps: array of strings describing what's missing
- suggestedAdditionalSearches: array of search queries to fill gaps (empty if none needed)
- requiresMoreResearch: boolean (true if confidence < 0.7)

Example:
{{
  ""confidenceScore"": 0.85,
  ""identifiedGaps"": [""Recent 2024 benchmarks missing""],
  ""suggestedAdditionalSearches"": [""GPT-4 2024 benchmarks""],
  ""requiresMoreResearch"": false
}}

Provide your evaluation:";

        try
        {
            var result = await _llmService.GetStructuredOutput<ReflectionResult>(prompt, "gpt-4o-mini", cancellationToken);

            if (result != null)
            {
                _logger.LogInformation("Reflection confidence: {Score:F2}, Requires more research: {Requires}", 
                    result.ConfidenceScore, result.RequiresMoreResearch);
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get structured reflection result");
        }

        // Fallback: simple heuristic-based reflection
        var citationCount = CountCitations(synthesizedResponse);
        var wordCount = synthesizedResponse.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        var confidence = CalculateHeuristicConfidence(citationCount, wordCount, info.TotalSourcesFound);

        return new ReflectionResult
        {
            ConfidenceScore = confidence,
            IdentifiedGaps = confidence < 0.7 
                ? new[] { "Response may lack sufficient detail or citations" } 
                : Array.Empty<string>(),
            SuggestedAdditionalSearches = Array.Empty<string>(),
            RequiresMoreResearch = confidence < 0.7
        };
    }

    private string GetEvaluationGuidance(int derpificationLevel)
    {
        if (derpificationLevel <= 33)
        {
            // Derp mode: Simple evaluation criteria
            return @"Evaluation Style: SIMPLE (Derp Mode - Elementary Level)
- Check if answer is easy to understand for kids
- Make sure it uses simple words
- Don't worry about missing advanced details
- Focus on: Did it answer the basic question?
- Be LESS strict - simple answers are okay
- Only suggest more research if the main idea is missing";
        }
        else if (derpificationLevel <= 66)
        {
            // Average mode: Balanced evaluation
            return @"Evaluation Style: BALANCED (Average Mode)
- Check for reasonable coverage of the topic
- Look for adequate citations and sources
- Expect moderate detail and clarity
- Standard expectations for completeness";
        }
        else
        {
            // Smart mode: Rigorous evaluation
            return @"Evaluation Style: RIGOROUS (Smart Mode)
- Check for comprehensive, thorough coverage
- Expect detailed citations and academic rigor
- Look for nuanced analysis and depth
- Identify any gaps in advanced concepts or edge cases
- Be MORE strict - expect high-quality, complete answers";
        }
    }

    private int CountCitations(string text)
    {
        var count = 0;
        for (int i = 1; i <= 20; i++)
        {
            if (text.Contains($"[{i}]"))
            {
                count++;
            }
        }
        return count;
    }

    private float CalculateHeuristicConfidence(int citations, int wordCount, int sourcesAvailable)
    {
        float score = 0.5f; // Base score

        // Citation factor (0-0.3 points)
        if (citations > 0)
        {
            score += Math.Min(0.3f, citations * 0.05f);
        }

        // Word count factor (0-0.2 points)
        if (wordCount >= 100)
        {
            score += Math.Min(0.2f, (wordCount / 500.0f) * 0.2f);
        }

        // Sources available factor
        if (sourcesAvailable >= 5)
        {
            score += 0.1f;
        }

        return Math.Min(1.0f, score);
    }
}
