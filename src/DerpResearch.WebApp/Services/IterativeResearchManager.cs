using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace DeepResearch.WebApp.Services;

/// <summary>
/// Manages the iterative research loop with reflection and additional searches.
/// Extracted from OrchestratorService to follow Single Responsibility Principle.
/// </summary>
public class IterativeResearchManager : IIterativeResearchManager
{
    private readonly ISynthesisAgent _synthesisAgent;
    private readonly IReflectionAgent _reflectionAgent;
    private readonly ISearchAgent _searchAgent;
    private readonly IMemoryService _memoryService;
    private readonly IProgressStreamingService _progressStreaming;
    private readonly ReflectionConfiguration _reflectionConfig;
    private readonly ILogger<IterativeResearchManager> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public IterativeResearchManager(
        ISynthesisAgent synthesisAgent,
        IReflectionAgent reflectionAgent,
        ISearchAgent searchAgent,
        IMemoryService memoryService,
        IProgressStreamingService progressStreaming,
        IOptions<ReflectionConfiguration> reflectionConfig,
        ILogger<IterativeResearchManager> logger)
    {
        _synthesisAgent = synthesisAgent;
        _reflectionAgent = reflectionAgent;
        _searchAgent = searchAgent;
        _memoryService = memoryService;
        _progressStreaming = progressStreaming;
        _reflectionConfig = reflectionConfig.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> ExecuteIterativeResearchAsync(
        string enhancedPrompt,
        ResearchPlan plan,
        GatheredInformation initialInfo,
        string conversationId,
        int derpificationLevel,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var maxIterations = _reflectionConfig.MaxIterations;
        
        string synthesizedResponse = "";
        ReflectionResult? reflection = null;
        int iteration = 0;
        var allResults = initialInfo.Results.ToList();
        var allMemoryIds = initialInfo.StoredMemoryIds.ToList();

        // Search for relevant memories
        var relevantMemories = await _memoryService.SearchMemoryAsync(enhancedPrompt, 5, conversationId, cancellationToken);

        while (iteration < maxIterations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            iteration++;
            
            // Synthesize response
            if (iteration == 1)
            {
                yield return _progressStreaming.CreateProgressToken(conversationId, "synthesizing", "Synthesizing comprehensive answer...");
            }
            else
            {
                yield return _progressStreaming.CreateProgressToken(conversationId, "synthesizing", $"Re-synthesizing with additional research (iteration {iteration})...");
            }

            var currentInfo = new GatheredInformation
            {
                Results = allResults.ToArray(),
                StoredMemoryIds = allMemoryIds.ToArray(),
                TotalSourcesFound = allResults.Count
            };

            var responseBuilder = new StringBuilder();
            await foreach (var token in _synthesisAgent.SynthesizeAsync(enhancedPrompt, plan, currentInfo, relevantMemories, derpificationLevel, cancellationToken).WithCancellation(cancellationToken))
            {
                responseBuilder.Append(token);
                yield return token;
            }

            synthesizedResponse = responseBuilder.ToString();

            // Reflect on the synthesized response
            reflection = await _reflectionAgent.ReflectAsync(enhancedPrompt, synthesizedResponse, currentInfo, derpificationLevel, cancellationToken);

            _logger.LogInformation(
                "Reflection iteration {Iteration}: Confidence={Confidence:F2}, RequiresMore={RequiresMore}, Suggestions={SuggestionCount}",
                iteration, reflection.ConfidenceScore, reflection.RequiresMoreResearch, reflection.SuggestedAdditionalSearches.Length);

            // Check if we should continue iterating
            if (!reflection.RequiresMoreResearch || 
                reflection.SuggestedAdditionalSearches.Length == 0 || 
                iteration >= maxIterations)
            {
                break;
            }

            // Perform additional searches based on reflection
            yield return "\n\n---\n\n";
            yield return $"🔄 **Confidence: {reflection.ConfidenceScore:F2} - Conducting additional research...**\n\n";
            
            foreach (var gap in reflection.IdentifiedGaps)
            {
                yield return $"*Identified gap: {gap}*\n";
            }
            yield return "\n";

            // Execute additional searches
            await foreach (var token in ExecuteAdditionalSearchesAsync(
                reflection.SuggestedAdditionalSearches,
                allResults,
                allMemoryIds,
                conversationId,
                derpificationLevel,
                cancellationToken).WithCancellation(cancellationToken))
            {
                yield return token;
            }

            yield return _progressStreaming.CreateProgressToken(
                conversationId, 
                "sources_complete", 
                $"Total sources: {allResults.Count} (+{allResults.Count - initialInfo.TotalSourcesFound} from iteration {iteration})");

            // Clear the previous synthesis for next iteration
            yield return "\n\n---\n\n";
        }

        // Log final reflection results and send to frontend
        if (reflection != null)
        {
            _logger.LogInformation(
                "Research completed after {Iterations} iteration(s). Final confidence: {Confidence:F2}",
                iteration, reflection.ConfidenceScore);
            
            // Build reasoning text from identified gaps
            var reasoning = reflection.IdentifiedGaps.Length > 0
                ? string.Join("; ", reflection.IdentifiedGaps)
                : reflection.ConfidenceScore >= 0.7
                    ? "Research appears comprehensive with adequate sources and citations."
                    : "Response may benefit from additional detail or sources.";
            
            yield return _progressStreaming.CreateReflectionToken(conversationId, reflection.ConfidenceScore, reasoning, iteration);
        }

        // Save assistant response
        await _memoryService.SaveMessageAsync(conversationId, "assistant", synthesizedResponse, cancellationToken);

        // Store the final synthesized response as a memory for future reference
        var storeResult = await _memoryService.StoreMemoryAsync(
            synthesizedResponse,
            "deep-research-synthesis",
            new[] { "synthesis", "deep-research", enhancedPrompt },
            conversationId,
            cancellationToken
        );
        
        if (!storeResult.IsFullySuccessful)
        {
            _logger.LogWarning("Synthesis memory storage had issues: {Success}/{Total} chunks succeeded", 
                storeResult.SuccessfulChunks, storeResult.TotalChunks);
        }
    }

    private async IAsyncEnumerable<string> ExecuteAdditionalSearchesAsync(
        string[] searchQueries,
        List<SearchResult> allResults,
        List<string> allMemoryIds,
        string conversationId,
        int derpificationLevel,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var additionalSearchCount = 0;
        foreach (var searchQuery in searchQueries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            additionalSearchCount++;
            
            yield return _progressStreaming.CreateSearchQueryToken(conversationId, searchQuery, additionalSearchCount, searchQueries.Length);

            // Create a simple plan with single search query for SearchAgent
            var additionalPlan = new ResearchPlan
            {
                MainGoal = searchQuery,
                Subtasks = new[]
                {
                    new ResearchTask
                    {
                        Description = searchQuery,
                        SearchQuery = searchQuery,
                        Priority = 1
                    }
                }
            };

            // Use SearchAgent to fetch with full webpage content and stream sources
            var existingUrls = new HashSet<string>(allResults.Select(r => r.Url));
            var newResults = new List<SearchResult>();
            GatheredInformation? additionalInfo = null;
            
            await foreach (var item in _searchAgent.ExecuteSearchPlanAsync(additionalPlan, derpificationLevel, cancellationToken).WithCancellation(cancellationToken))
            {
                if (item is SearchResult result)
                {
                    // Only add if not duplicate
                    if (!existingUrls.Contains(result.Url))
                    {
                        newResults.Add(result);
                        allResults.Add(result);
                        
                        yield return _progressStreaming.CreateSourceToken(conversationId, result.Title, result.Url, result.Snippet);
                    }
                }
                else if (item is GatheredInformation gatheredInfo)
                {
                    additionalInfo = gatheredInfo;
                }
            }
            
            if (additionalInfo != null)
            {
                allMemoryIds.AddRange(additionalInfo.StoredMemoryIds);
            }
            
            _logger.LogInformation("Additional search '{Query}' found {NewCount} new sources", 
                searchQuery, newResults.Count);
        }
    }
}
