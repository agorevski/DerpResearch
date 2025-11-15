using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using System.Text;
using System.Text.Json;

namespace DeepResearch.WebApp.Services;

public class OrchestratorService : IOrchestratorService
{
    private readonly IClarificationAgent _clarificationAgent;
    private readonly IPlannerAgent _plannerAgent;
    private readonly ISearchAgent _searchAgent;
    private readonly ISynthesisAgent _synthesisAgent;
    private readonly IReflectionAgent _reflectionAgent;
    private readonly IMemoryService _memoryService;
    private readonly ISearchService _searchService;
    private readonly ILLMService _llmService;
    private readonly IConfiguration _config;
    private readonly ILogger<OrchestratorService> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OrchestratorService(
        IClarificationAgent clarificationAgent,
        IPlannerAgent plannerAgent,
        ISearchAgent searchAgent,
        ISynthesisAgent synthesisAgent,
        IReflectionAgent reflectionAgent,
        IMemoryService memoryService,
        ISearchService searchService,
        ILLMService llmService,
        IConfiguration config,
        ILogger<OrchestratorService> logger)
    {
        _clarificationAgent = clarificationAgent;
        _plannerAgent = plannerAgent;
        _searchAgent = searchAgent;
        _synthesisAgent = synthesisAgent;
        _reflectionAgent = reflectionAgent;
        _memoryService = memoryService;
        _searchService = searchService;
        _llmService = llmService;
        _config = config;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> ProcessDeepResearchAsync(
        string prompt, 
        string conversationId, 
        int derpificationLevel = 100, 
        string[]? clarificationAnswers = null)
    {
        _logger.LogInformation("Starting deep research for conversation {ConversationId} with derpification level {Level}, has answers: {HasAnswers}", 
            conversationId, derpificationLevel, clarificationAnswers != null);

        // Save user message
        await _memoryService.SaveMessageAsync(conversationId, "user", prompt);

        // Step 1: Get conversation context
        var context = await _memoryService.GetConversationContextAsync(conversationId);

        // Step 1.5: PHASE 1 - Generate clarifying questions if no answers provided yet
        if (clarificationAnswers == null || clarificationAnswers.Length == 0)
        {
            _logger.LogInformation("Phase 1: Generating clarifying questions");
            
            yield return JsonSerializer.Serialize(new StreamToken(
                "",
                conversationId,
                "progress",
                new ProgressUpdate("clarifying", "Understanding your research needs...")
            ), _jsonOptions) + "\n";

            var clarification = await _clarificationAgent.GenerateClarifyingQuestionsAsync(prompt, context, derpificationLevel);
            
            yield return JsonSerializer.Serialize(new StreamToken(
                "",
                conversationId,
                "clarification",
                new ClarificationUpdate(clarification.Questions, clarification.Rationale)
            ), _jsonOptions) + "\n";

            _logger.LogInformation("Generated {Count} clarifying questions. Waiting for user answers.", clarification.Questions.Length);
            
            // Store questions in database for Phase 2
            await _memoryService.StoreClarificationQuestionsAsync(conversationId, clarification.Questions);
            
            // STOP HERE - Wait for user to provide answers
            // Frontend should re-submit with clarificationAnswers populated
            yield break;
        }

        // Step 1.6: PHASE 2 - Enhance prompt with clarification answers
        _logger.LogInformation("Phase 2: Processing with {Count} clarification answers", clarificationAnswers.Length);
        
        // Retrieve the questions from Phase 1 (from database)
        var questions = await _memoryService.GetClarificationQuestionsAsync(conversationId) ?? Array.Empty<string>();
        
        var enhancedPrompt = EnhancePromptWithClarifications(prompt, questions, clarificationAnswers);
        _logger.LogInformation("Enhanced prompt created from {Count} Q&A pairs", questions.Length);
        
        // Clean up clarification questions after use
        await _memoryService.ClearClarificationQuestionsAsync(conversationId);

        // Step 2: Create research plan (using enhanced prompt)
        yield return JsonSerializer.Serialize(new StreamToken(
            "",
            conversationId,
            "progress",
            new ProgressUpdate("planning", "Analyzing your question and creating research plan...")
        ), _jsonOptions) + "\n";

        var plan = await _plannerAgent.CreatePlanAsync(enhancedPrompt, context, derpificationLevel);
        
        yield return JsonSerializer.Serialize(new StreamToken(
            "",
            conversationId,
            "plan",
            new { goal = plan.MainGoal, subtasks = plan.Subtasks.Select(t => t.Description).ToArray() }
        ), _jsonOptions) + "\n";

        // Step 3: Execute search plan with progress updates
        yield return JsonSerializer.Serialize(new StreamToken(
            "",
            conversationId,
            "progress",
            new ProgressUpdate("searching", "Gathering information from web sources...")
        ), _jsonOptions) + "\n";

        GatheredInformation? info = null;
        await foreach (var update in ExecuteSearchWithProgressAsync(plan, conversationId, derpificationLevel))
        {
            // Check if this is the final GatheredInformation marker
            if (update.StartsWith("FINAL_INFO:"))
            {
                var jsonData = update.Substring(11); // Remove "FINAL_INFO:" prefix
                info = System.Text.Json.JsonSerializer.Deserialize<GatheredInformation>(jsonData)!;
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
            yield return JsonSerializer.Serialize(new StreamToken(
                "",
                conversationId,
                "progress",
                new ProgressUpdate("fallback", "No sources found. Using general knowledge to answer.")
            ), _jsonOptions) + "\n";
            
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
            await foreach (var token in _llmService.ChatCompletionStream(messages.ToArray(), "gpt-4o"))
            {
                responseBuilder.Append(token);
                yield return token;
            }

            var response = responseBuilder.ToString();
            await _memoryService.SaveMessageAsync(conversationId, "assistant", response);
            
            _logger.LogInformation("Completed with fallback response for conversation {ConversationId}", conversationId);
            yield break;
        }

        yield return JsonSerializer.Serialize(new StreamToken(
            "",
            conversationId,
            "progress",
            new ProgressUpdate("sources_complete", $"Found {info.TotalSourcesFound} sources")
        ), _jsonOptions) + "\n";

        // Step 4: Search for relevant memories
        var relevantMemories = await _memoryService.SearchMemoryAsync(enhancedPrompt, 5, conversationId);

        // Step 5-6: Synthesize and Reflect (with iteration loop)
        var maxIterations = int.Parse(_config["Reflection:MaxIterations"] ?? "2");
        var confidenceThreshold = double.Parse(_config["Reflection:ConfidenceThreshold"] ?? "0.7");
        
        string synthesizedResponse = "";
        ReflectionResult? reflection = null;
        int iteration = 0;
        var allResults = info.Results.ToList();
        var allMemoryIds = info.StoredMemoryIds.ToList();

        while (iteration < maxIterations)
        {
            iteration++;
            
            // Synthesize response
            if (iteration == 1)
            {
                yield return JsonSerializer.Serialize(new StreamToken(
                    "",
                    conversationId,
                    "progress",
                    new ProgressUpdate("synthesizing", "Synthesizing comprehensive answer...")
                ), _jsonOptions) + "\n";
            }
            else
            {
                yield return JsonSerializer.Serialize(new StreamToken(
                    "",
                    conversationId,
                    "progress",
                    new ProgressUpdate("synthesizing", $"Re-synthesizing with additional research (iteration {iteration})...")
                ), _jsonOptions) + "\n";
            }

            var currentInfo = new GatheredInformation
            {
                Results = allResults.ToArray(),
                StoredMemoryIds = allMemoryIds.ToArray(),
                TotalSourcesFound = allResults.Count
            };

            var responseBuilder = new StringBuilder();
            await foreach (var token in _synthesisAgent.SynthesizeAsync(enhancedPrompt, plan, currentInfo, relevantMemories, derpificationLevel))
            {
                responseBuilder.Append(token);
                yield return token;
            }

            synthesizedResponse = responseBuilder.ToString();

            // Reflect on the synthesized response
            reflection = await _reflectionAgent.ReflectAsync(enhancedPrompt, synthesizedResponse, currentInfo, derpificationLevel);

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

            // Execute additional searches using SearchAgent to fetch full webpage content
            var additionalSearchCount = 0;
            foreach (var searchQuery in reflection.SuggestedAdditionalSearches)
            {
                additionalSearchCount++;
                
                yield return JsonSerializer.Serialize(new StreamToken(
                    "",
                    conversationId,
                    "search_query",
                    new SearchQueryUpdate(searchQuery, additionalSearchCount, reflection.SuggestedAdditionalSearches.Length)
                ), _jsonOptions) + "\n";

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
                
                await foreach (var item in _searchAgent.ExecuteSearchPlanAsync(additionalPlan, derpificationLevel).ConfigureAwait(false))
                {
                    if (item is SearchResult result)
                    {
                        // Only add if not duplicate
                        if (!existingUrls.Contains(result.Url))
                        {
                            newResults.Add(result);
                            allResults.Add(result);
                            
                            // Yield source immediately
                            yield return JsonSerializer.Serialize(new StreamToken(
                                "",
                                conversationId,
                                "source",
                                new SourceUpdate(result.Title, result.Url, result.Snippet)
                            ), _jsonOptions) + "\n";
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

            yield return JsonSerializer.Serialize(new StreamToken(
                "",
                conversationId,
                "progress",
                new ProgressUpdate("sources_complete", $"Total sources: {allResults.Count} (+{allResults.Count - info.TotalSourcesFound} from iteration {iteration})")
            ), _jsonOptions) + "\n";

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
            
            // Send reflection data to frontend
            yield return JsonSerializer.Serialize(new StreamToken(
                "",
                conversationId,
                "reflection",
                new ReflectionUpdate(reflection.ConfidenceScore, reasoning, iteration)
            ), _jsonOptions) + "\n";
        }

        // Step 7: Save assistant response
        await _memoryService.SaveMessageAsync(conversationId, "assistant", synthesizedResponse);

        // Store the final synthesized response as a memory for future reference
        await _memoryService.StoreMemoryAsync(
            synthesizedResponse,
            "deep-research-synthesis",
            new[] { "synthesis", "deep-research", enhancedPrompt },
            conversationId
        );

        _logger.LogInformation("Deep research completed for conversation {ConversationId}", conversationId);
    }

    private string EnhancePromptWithClarifications(string originalPrompt, string[] questions, string[] clarificationAnswers)
    {
        var enhanced = new StringBuilder();
        enhanced.AppendLine($"Original Question: {originalPrompt}");
        enhanced.AppendLine();
        enhanced.AppendLine("Clarifying Q&A:");
        
        // Match questions with answers
        int maxIndex = Math.Min(questions.Length, clarificationAnswers.Length);
        for (int i = 0; i < maxIndex; i++)
        {
            if (!string.IsNullOrWhiteSpace(clarificationAnswers[i]))
            {
                enhanced.AppendLine($"Q: {questions[i]}");
                enhanced.AppendLine($"A: {clarificationAnswers[i]}");
                enhanced.AppendLine();
            }
        }
        
        enhanced.AppendLine("Please conduct research that addresses the original question considering the clarifications provided above.");
        
        return enhanced.ToString();
    }

    private async IAsyncEnumerable<string> ExecuteSearchWithProgressAsync(ResearchPlan plan, string conversationId, int derpificationLevel)
    {
        int taskNumber = 0;
        int totalTasks = plan.Subtasks.Length;

        foreach (var task in plan.Subtasks)
        {
            taskNumber++;
            
            // Yield search query update
            yield return JsonSerializer.Serialize(new StreamToken(
                "",
                conversationId,
                "search_query",
                new SearchQueryUpdate(task.SearchQuery, taskNumber, totalTasks)
            ), _jsonOptions) + "\n";
        }

        // Use SearchAgent which handles fetching webpage content and streams sources
        GatheredInformation? info = null;
        bool hasError = false;
        
        await foreach (var item in _searchAgent.ExecuteSearchPlanAsync(plan, derpificationLevel).ConfigureAwait(false))
        {
            // Check if this is a SearchResult or GatheredInformation
            if (item is SearchResult result)
            {
                // Yield source immediately as it's fetched
                yield return JsonSerializer.Serialize(new StreamToken(
                    "",
                    conversationId,
                    "source",
                    new SourceUpdate(result.Title, result.Url, result.Snippet)
                ), _jsonOptions) + "\n";
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
        yield return "FINAL_INFO:" + System.Text.Json.JsonSerializer.Serialize(info);
    }
}
