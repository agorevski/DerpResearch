using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DeepResearch.WebApp.Services;
using Microsoft.Extensions.Options;
using System.Text;
using System.Runtime.CompilerServices;

namespace DeepResearch.WebApp.Agents;

public class SynthesisAgent : ISynthesisAgent
{
    private readonly ILLMService _llmService;
    private readonly ILogger<SynthesisAgent> _logger;
    private readonly AzureOpenAIConfiguration _aiConfig;

    public SynthesisAgent(ILLMService llmService, ILogger<SynthesisAgent> logger, IOptions<AzureOpenAIConfiguration> aiConfig)
    {
        _llmService = llmService;
        _logger = logger;
        _aiConfig = aiConfig.Value;
    }

    public async IAsyncEnumerable<string> SynthesizeAsync(
        string userQuery,
        ResearchPlan plan,
        GatheredInformation info,
        MemoryChunk[] relevantMemories,
        int derpificationLevel = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        userQuery = PromptSanitizer.Sanitize(userQuery);
        
        var prompt = BuildSynthesisPrompt(userQuery, plan, info, relevantMemories, derpificationLevel);

        var messages = new[]
        {
            new ChatMessage 
            { 
                Role = "system", 
                Content = "You are a research synthesizer. Create comprehensive, well-cited responses based on gathered information. Use [1], [2], etc. for citations." 
            },
            new ChatMessage 
            { 
                Role = "user", 
                Content = prompt 
            }
        };

        _logger.LogInformation("Starting synthesis for query: {Query}", userQuery);

        await foreach (var token in _llmService.ChatCompletionStream(messages, _aiConfig.Deployments.Chat, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return token;
        }
    }

    private string BuildSynthesisPrompt(
        string userQuery,
        ResearchPlan plan,
        GatheredInformation info,
        MemoryChunk[] relevantMemories,
        int derpificationLevel)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"User Question: \"{userQuery}\"");
        sb.AppendLine();
        sb.AppendLine($"Research Goal: {plan.MainGoal}");
        sb.AppendLine();
        sb.AppendLine(GetSynthesisStyleGuidance(derpificationLevel));
        sb.AppendLine();

        // Add gathered sources
        if (info.Results.Length > 0)
        {
            sb.AppendLine("Sources Gathered:");
            for (int i = 0; i < info.Results.Length; i++)
            {
                var result = info.Results[i];
                sb.AppendLine($"[{i + 1}] {result.Title}");
                sb.AppendLine($"    URL: {result.Url}");
                sb.AppendLine($"    Summary: {result.Snippet}");
                sb.AppendLine();
            }
        }

        // Add relevant memories from previous research
        if (relevantMemories.Length > 0)
        {
            sb.AppendLine("Relevant Context from Previous Research:");
            foreach (var memory in relevantMemories)
            {
                sb.AppendLine($"- {memory.Text.Substring(0, Math.Min(200, memory.Text.Length))}...");
                sb.AppendLine($"  Source: {memory.Source}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("General Instructions:");
        sb.AppendLine("1. Write a response that directly answers the user's question");
        sb.AppendLine("2. Cite sources using [1], [2], etc. matching the numbered sources above");
        sb.AppendLine("3. Organize information logically");
        sb.AppendLine("4. Use markdown formatting for better readability (headers, lists, bold, etc.)");
        sb.AppendLine("5. Be factual, accurate, and cite sources for all claims");
        sb.AppendLine();
        sb.AppendLine("Begin your response:");

        return sb.ToString();
    }

    private string GetSynthesisStyleGuidance(int derpificationLevel)
    {
        if (derpificationLevel <= DerpificationConstants.DerpMaxLevel)
        {
            // Derp mode: SUPER SIMPLE - like an elementary school report
            return @"Response Style: SUPER SIMPLE (Derp Mode - Elementary School Report)
- Write like you're explaining to a 10-year-old child
- Use ONLY simple words (avoid ANY fancy or technical words)
- Keep sentences VERY SHORT (5-10 words each)
- Use LOTS of basic examples kids can understand
- Write in a fun, friendly way (like talking to a friend)
- Make paragraphs tiny (1-2 sentences only)
- Use simple comparisons (""it's like..."", ""imagine if..."")
- Pretend you're a student doing a homework report
- NO complex ideas - just the basics that are easy to understand";
        }
        else if (derpificationLevel <= DerpificationConstants.AverageMaxLevel)
        {
            // Average mode: Balanced, professional
            return @"Response Style: BALANCED & PROFESSIONAL (Average Mode)
- Use clear, professional language
- Provide moderate detail with good explanations
- Balance accessibility with technical accuracy
- Include relevant context and background
- Structure with clear sections and transitions
- Use standard paragraph length (3-5 sentences)
- Define technical terms when introduced";
        }
        else
        {
            // Smart mode: Comprehensive, academic
            return @"Response Style: COMPREHENSIVE & ACADEMIC (Smart Mode)
- Use precise, technical language and domain-specific terminology
- Provide DETAILED, THOROUGH explanations with nuanced insights
- Include advanced concepts, edge cases, and implications
- Analyze multiple perspectives and dimensions
- Structure with sophisticated organization and clear hierarchy
- Use longer, well-developed paragraphs with supporting details
- Include academic rigor with comprehensive citations
- Discuss limitations, caveats, and areas requiring further research
- Highlight connections between concepts and broader context";
        }
    }
}
