using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Agents;

public class ClarificationAgent : IClarificationAgent
{
    private readonly ILLMService _llmService;
    private readonly ILogger<ClarificationAgent> _logger;

    public ClarificationAgent(ILLMService llmService, ILogger<ClarificationAgent> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<ClarificationResult> GenerateClarifyingQuestionsAsync(
        string userQuery,
        ConversationContext context,
        int derpificationLevel = 100,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var contextSummary = BuildContextSummary(context);
        var questionGuidance = GetQuestionGuidance(derpificationLevel);

        var prompt = $@"You are a research assistant helping to understand the user's research needs better.

{questionGuidance}

User's Initial Query: ""{userQuery}""

{contextSummary}

Generate clarifying questions that will help you better understand:
- What specific aspects they're most interested in
- The depth/scope of information they need
- Any particular focus areas or constraints
- Context for why they're researching this

Return ONLY a valid JSON object with:
{{
  ""questions"": [""question 1"", ""question 2"", ...],
  ""rationale"": ""Brief explanation of why these questions help""
}}

The questions should be open-ended and help narrow down the research scope.";

        try
        {
            var result = await _llmService.GetStructuredOutput<ClarificationResult>(prompt, "gpt-4o", cancellationToken);

            if (result != null && result.Questions.Length > 0)
            {
                _logger.LogInformation("Generated {Count} clarifying questions for query: {Query}", 
                    result.Questions.Length, userQuery);
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate clarifying questions");
            _logger.LogError("Attempted to use deployment: gpt-4o for clarification questions");
        }

        // Fallback: Generate simple heuristic questions based on derpification level
        return GenerateFallbackQuestions(userQuery, derpificationLevel);
    }

    private string GetQuestionGuidance(int derpificationLevel)
    {
        if (derpificationLevel <= 33)
        {
            // Derp mode: Super simple questions
            return @"Question Style: SUPER SIMPLE (Derp Mode - Elementary Level)
- Generate 1-2 VERY BASIC questions
- Use simple words a 10-year-old would understand
- Ask simple things like ""What do you want to learn?"" or ""Why are you curious?""
- Keep questions SHORT and FRIENDLY
- No fancy words or complicated ideas";
        }
        else if (derpificationLevel <= 66)
        {
            // Average mode: Focused questions
            return @"Question Style: FOCUSED (Average Mode)
- Generate 2-3 clarifying questions
- Use clear, professional language
- Focus on scope, specific aspects, and timeframe
- Questions should help narrow down the research focus
- Examples: ""What specific aspects interest you most?"" or ""Are you looking for recent or historical information?""";
        }
        else
        {
            // Smart mode: Nuanced questions
            return @"Question Style: NUANCED (Smart Mode - Academic/Professional)
- Generate 3-4 detailed, probing questions
- Use precise, academic language
- Explore temporal scope, analytical framework, and methodological preferences
- Questions should uncover subtle distinctions in research intent
- Examples: ""What is the temporal and geographical scope?"" or ""Are you seeking comparative analysis, causal relationships, or descriptive synthesis?""";
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

        return summary;
    }

    private ClarificationResult GenerateFallbackQuestions(string userQuery, int derpificationLevel)
    {
        string[] questions;
        string rationale;

        if (derpificationLevel <= 33)
        {
            // Derp mode fallback
            questions = new[]
            {
                "What do you want to learn about?",
                "Why are you curious about this?"
            };
            rationale = "These simple questions help us understand what you're looking for.";
        }
        else if (derpificationLevel <= 66)
        {
            // Average mode fallback
            questions = new[]
            {
                "What specific aspects of this topic are you most interested in?",
                "Are you looking for recent information or historical context?",
                "What will you use this research for?"
            };
            rationale = "These questions help narrow down the scope and focus of the research.";
        }
        else
        {
            // Smart mode fallback
            questions = new[]
            {
                "What is the temporal and geographical scope of your inquiry?",
                "Are you seeking comparative analysis, causal relationships, or descriptive synthesis?",
                "What level of technical depth do you require?",
                "Are there specific methodologies or frameworks you prefer?"
            };
            rationale = "These questions help establish the analytical framework and methodological boundaries for comprehensive research.";
        }

        _logger.LogWarning("Using fallback clarifying questions for query: {Query}", userQuery);

        return new ClarificationResult
        {
            Questions = questions,
            Rationale = rationale
        };
    }
}
