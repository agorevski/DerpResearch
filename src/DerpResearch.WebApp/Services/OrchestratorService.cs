using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;

namespace DeepResearch.WebApp.Services;

/// <summary>
/// Thin workflow coordinator that delegates to specialized services.
/// Refactored from God Object to follow Single Responsibility Principle.
/// </summary>
public class OrchestratorService : IOrchestratorService
{
    private readonly IPlannerAgent _plannerAgent;
    private readonly ISearchAgent _searchAgent;
    private readonly IMemoryService _memoryService;
    private readonly ILLMService _llmService;
    private readonly IProgressStreamingService _progressStreaming;
    private readonly IClarificationManager _clarificationManager;
    private readonly IIterativeResearchManager _iterativeResearchManager;
    private readonly ILogger<OrchestratorService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OrchestratorService(
        IPlannerAgent plannerAgent,
        ISearchAgent searchAgent,
        IMemoryService memoryService,
        ILLMService llmService,
        IProgressStreamingService progressStreaming,
        IClarificationManager clarificationManager,
        IIterativeResearchManager iterativeResearchManager,
        ILogger<OrchestratorService> logger)
    {
        _plannerAgent = plannerAgent;
        _searchAgent = searchAgent;
        _memoryService = memoryService;
        _llmService = llmService;
        _progressStreaming = progressStreaming;
        _clarificationManager = clarificationManager;
        _iterativeResearchManager = iterativeResearchManager;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> ProcessDeepResearchAsync(
        string prompt, 
        string conversationId, 
        int derpificationLevel = 100, 
        string[]? clarificationAnswers = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        _logger.LogInformation("Starting deep research for conversation {ConversationId} with derpification level {Level}, has answers: {HasAnswers}", 
            conversationId, derpificationLevel, clarificationAnswers != null);

        // Save user message
        await _memoryService.SaveMessageAsync(conversationId, "user", prompt, cancellationToken);

        // Step 1: Get conversation context
        var context = await _memoryService.GetConversationContextAsync(conversationId, cancellationToken: cancellationToken);

        // Step 1.5: PHASE 1 - Generate clarifying questions if no answers provided yet
        if (clarificationAnswers == null || clarificationAnswers.Length == 0)
        {
            _logger.LogInformation("Phase 1: Generating clarifying questions");
            
            yield return _progressStreaming.CreateProgressToken(conversationId, "clarifying", "Understanding your research needs...");

            var clarification = await _clarificationManager.GenerateClarificationAsync(prompt, conversationId, context.ToString() ?? "", derpificationLevel, clarificationAnswers, cancellationToken);
            
            if (clarification != null)
            {
                yield return _progressStreaming.CreateClarificationToken(conversationId, clarification.Questions, clarification.Rationale);
                _logger.LogInformation("Generated {Count} clarifying questions. Waiting for user answers.", clarification.Questions.Length);
                yield break;
            }
        }

        // Step 1.6: PHASE 2 - Enhance prompt with clarification answers
        _logger.LogInformation("Phase 2: Processing with {Count} clarification answers", clarificationAnswers?.Length ?? 0);
        
        var enhancedPrompt = await _clarificationManager.EnhancePromptWithClarificationsAsync(prompt, conversationId, clarificationAnswers ?? Array.Empty<string>(), cancellationToken);

        // Step 2: Create research plan (using enhanced prompt)
        yield return _progressStreaming.CreateProgressToken(conversationId, "planning", "Analyzing your question and creating research plan...");

        var plan = await _plannerAgent.CreatePlanAsync(enhancedPrompt, context, derpificationLevel, cancellationToken);
        
        yield return _progressStreaming.CreatePlanToken(conversationId, plan.MainGoal, plan.Subtasks.Select(t => t.Description).ToArray());

        // Step 3: Execute search plan with progress updates
        yield return _progressStreaming.CreateProgressToken(conversationId, "searching", "Gathering information from web sources...");

        GatheredInformation? info = null;
        await foreach (var update in ExecuteSearchWithProgressAsync(plan, conversationId, derpificationLevel, cancellationToken).WithCancellation(cancellationToken))
        {
            // Check if this is the final GatheredInformation marker
            if (update.StartsWith("FINAL_INFO:"))
            {
                var jsonData = update.Substring(11); // Remove "FINAL_INFO:" prefix
                info = JsonSerializer.Deserialize<GatheredInformation>(jsonData)!;
            }
            else
            {
                // Regular progress update - yield to client
                yield return update;
            }
        }
        
        if (info == null)
        {
            throw new InvalidOperationException("Search execution did not return GatheredInformation");
        }
        
        if (info.TotalSourcesFound == 0)
        {
            yield return _progressStreaming.CreateProgressToken(conversationId, "fallback", "No sources found. Using general knowledge to answer.");
            
            // Fallback to direct LLM response when no sources found
            var messages = new List<ChatMessage>
            {
                new ChatMessage 
                { 
                    Role = "system", 
                    Content = "You are a helpful AI assistant. Provide accurate, well-structured responses based on your knowledge." 
                }
            };

            messages.AddRange(context.RecentMessages);
            
            if (!context.RecentMessages.Any(m => m.Content == prompt))
            {
                messages.Add(new ChatMessage { Role = "user", Content = prompt });
            }

            var responseBuilder = new StringBuilder();
            await foreach (var token in _llmService.ChatCompletionStream(messages.ToArray(), "gpt-4o", cancellationToken).WithCancellation(cancellationToken))
            {
                responseBuilder.Append(token);
                yield return token;
            }

            var response = responseBuilder.ToString();
            await _memoryService.SaveMessageAsync(conversationId, "assistant", response, cancellationToken);
            
            _logger.LogInformation("Completed with fallback response for conversation {ConversationId}", conversationId);
            yield break;
        }

        yield return _progressStreaming.CreateProgressToken(conversationId, "sources_complete", $"Found {info.TotalSourcesFound} sources");

        // Step 4-6: Execute iterative research loop (synthesis, reflection, additional searches)
        await foreach (var token in _iterativeResearchManager.ExecuteIterativeResearchAsync(
            enhancedPrompt, plan, info, conversationId, derpificationLevel, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return token;
        }

        _logger.LogInformation("Deep research completed for conversation {ConversationId}", conversationId);
    }

    private async IAsyncEnumerable<string> ExecuteSearchWithProgressAsync(ResearchPlan plan, string conversationId, int derpificationLevel, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        int taskNumber = 0;
        int totalTasks = plan.Subtasks.Length;

        foreach (var task in plan.Subtasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            taskNumber++;
            
            // Yield search query update
            yield return _progressStreaming.CreateSearchQueryToken(conversationId, task.SearchQuery, taskNumber, totalTasks);
        }

        // Use SearchAgent which handles fetching webpage content and streams sources
        GatheredInformation? info = null;
        bool hasError = false;
        
        await foreach (var item in _searchAgent.ExecuteSearchPlanAsync(plan, derpificationLevel, cancellationToken).WithCancellation(cancellationToken))
        {
            // Check if this is a SearchResult or GatheredInformation
            if (item is SearchResult result)
            {
                // Yield source immediately as it's fetched
                yield return _progressStreaming.CreateSourceToken(conversationId, result.Title, result.Url, result.Snippet);
            }
            else if (item is GatheredInformation gatheredInfo)
            {
                // This is the final result
                info = gatheredInfo;
            }
        }

        // Ensure we have info
        if (info == null || hasError)
        {
            info = new GatheredInformation
            {
                Results = Array.Empty<SearchResult>(),
                StoredMemoryIds = Array.Empty<string>(),
                TotalSourcesFound = 0
            };
        }

        // Yield the final result with a special prefix so it's not sent to the client
        yield return "FINAL_INFO:" + JsonSerializer.Serialize(info);
    }
}
