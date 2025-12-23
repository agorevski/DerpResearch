using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using System.Text;

namespace DeepResearch.WebApp.Services;

/// <summary>
/// Manages clarification question generation and prompt enhancement.
/// Extracted from OrchestratorService to follow Single Responsibility Principle.
/// </summary>
public class ClarificationManager : IClarificationManager
{
    private readonly IClarificationAgent _clarificationAgent;
    private readonly IMemoryService _memoryService;
    private readonly ILogger<ClarificationManager> _logger;

    public ClarificationManager(
        IClarificationAgent clarificationAgent,
        IMemoryService memoryService,
        ILogger<ClarificationManager> logger)
    {
        _clarificationAgent = clarificationAgent;
        _memoryService = memoryService;
        _logger = logger;
    }

    public async Task<ClarificationResult?> GenerateClarificationAsync(
        string prompt,
        string conversationId,
        string context,
        int derpificationLevel,
        string[]? existingAnswers,
        CancellationToken cancellationToken = default)
    {
        // If answers already provided, skip clarification phase
        if (existingAnswers != null && existingAnswers.Length > 0)
        {
            _logger.LogInformation("Skipping clarification - answers already provided");
            return null;
        }

        _logger.LogInformation("Generating clarifying questions for conversation {ConversationId}", conversationId);
        
        var conversationContext = await _memoryService.GetConversationContextAsync(conversationId, cancellationToken: cancellationToken);
        var clarification = await _clarificationAgent.GenerateClarifyingQuestionsAsync(prompt, conversationContext, derpificationLevel, cancellationToken);
        
        // Store questions in database for Phase 2
        await _memoryService.StoreClarificationQuestionsAsync(conversationId, clarification.Questions, cancellationToken);
        
        _logger.LogInformation("Generated {Count} clarifying questions", clarification.Questions.Length);
        
        return new ClarificationResult { Questions = clarification.Questions, Rationale = clarification.Rationale };
    }

    public async Task<string> EnhancePromptWithClarificationsAsync(
        string originalPrompt,
        string conversationId,
        string[] clarificationAnswers,
        CancellationToken cancellationToken = default)
    {
        // Retrieve the questions from Phase 1 (from database)
        var questions = await _memoryService.GetClarificationQuestionsAsync(conversationId, cancellationToken) ?? Array.Empty<string>();
        
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
        
        _logger.LogInformation("Enhanced prompt created from {Count} Q&A pairs", questions.Length);
        
        // Clean up clarification questions after use
        await _memoryService.ClearClarificationQuestionsAsync(conversationId, cancellationToken);
        
        return enhanced.ToString();
    }
}
