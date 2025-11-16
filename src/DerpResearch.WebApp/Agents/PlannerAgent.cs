using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Agents;

public class PlannerAgent : IPlannerAgent
{
    private readonly ILLMService _llmService;
    private readonly ILogger<PlannerAgent> _logger;

    public PlannerAgent(ILLMService llmService, ILogger<PlannerAgent> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<ResearchPlan> CreatePlanAsync(string userQuery, ConversationContext context, int derpificationLevel = 100, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var contextSummary = BuildContextSummary(context);
        var complexityGuidance = GetComplexityGuidance(derpificationLevel);

        var prompt = $@"You are a research planner. Break down this query into specific research subtasks.

{complexityGuidance}

User Query: ""{userQuery}""

{contextSummary}

Create a detailed research plan with:
1. mainGoal: The overall objective (1-2 sentences)
2. subtasks: Array of specific research questions/tasks (3-7 items)
   - Each subtask should have:
     * description: What information to find
     * searchQuery: A search engine query to find this info
     * priority: 1 (highest) to 5 (lowest)
3. keyConcepts: Important terms or topics (3-10 items)

Think step-by-step about what information is needed to comprehensively answer the query.

Return ONLY a valid JSON object matching this structure:
{{
  ""mainGoal"": ""string"",
  ""subtasks"": [
    {{
      ""description"": ""string"",
      ""searchQuery"": ""string"",
      ""priority"": 1
    }}
  ],
  ""keyConcepts"": [""string"", ""string""]
}}";

        var plan = await _llmService.GetStructuredOutput<ResearchPlan>(prompt, "gpt-4o", cancellationToken);

        if (plan == null)
        {
            _logger.LogWarning("Failed to create structured plan, using fallback");
            return new ResearchPlan
            {
                MainGoal = userQuery,
                Subtasks = new[]
                {
                    new ResearchTask
                    {
                        Description = userQuery,
                        SearchQuery = userQuery,
                        Priority = 1
                    }
                },
                KeyConcepts = Array.Empty<string>()
            };
        }

        // Sort subtasks by priority
        plan.Subtasks = plan.Subtasks.OrderBy(t => t.Priority).ToArray();

        _logger.LogInformation("Created research plan with {Count} subtasks for: {Query}", 
            plan.Subtasks.Length, userQuery);

        return plan;
    }

    private string GetComplexityGuidance(int derpificationLevel)
    {
        if (derpificationLevel <= 33)
        {
            // Derp mode: SUPER SIMPLE - like a child doing research
            return @"Planning Style: SUPER SIMPLE (Derp Mode - Elementary School Level)
- Create only 2 VERY BASIC questions about the topic
- Use words a 10-year-old would understand
- Ask simple ""what is"" or ""why"" questions
- Make search queries as simple as possible (3-5 words max)
- Think like you're helping a young student with homework";
        }
        else if (derpificationLevel <= 66)
        {
            // Average mode: Balanced planning
            return @"Planning Style: BALANCED (Average Mode)
- Create 3-5 focused research subtasks
- Use clear, professional language
- Balance breadth and depth
- Formulate specific but accessible search queries";
        }
        else
        {
            // Smart mode: Comprehensive, detailed planning
            return @"Planning Style: COMPREHENSIVE (Smart Mode)
- Create 5-7 detailed, specific research subtasks
- Use precise, technical language where appropriate
- Cover all aspects thoroughly, including edge cases
- Formulate sophisticated, targeted search queries
- Include both foundational and advanced topics";
        }
    }

    private string BuildContextSummary(ConversationContext context)
    {
        if (context.RecentMessages.Length == 0 && context.RelevantMemories.Length == 0)
        {
            return "";
        }

        var summary = "Previous Context:\n";

        if (context.RecentMessages.Length > 0)
        {
            summary += "Recent conversation:\n";
            foreach (var msg in context.RecentMessages.TakeLast(3))
            {
                summary += $"- {msg.Role}: {msg.Content.Substring(0, Math.Min(100, msg.Content.Length))}...\n";
            }
        }

        if (context.RelevantMemories.Length > 0)
        {
            summary += "\nRelevant memories:\n";
            foreach (var mem in context.RelevantMemories.Take(3))
            {
                summary += $"- {mem.Text.Substring(0, Math.Min(100, mem.Text.Length))}...\n";
            }
        }

        return summary;
    }
}
