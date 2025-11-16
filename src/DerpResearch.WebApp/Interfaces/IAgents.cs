using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Interfaces;

public interface IPlannerAgent
{
    /// <summary>
    /// Break down user query into subtasks
    /// </summary>
    Task<ResearchPlan> CreatePlanAsync(string userQuery, ConversationContext context, int derpificationLevel = 100, CancellationToken cancellationToken = default);
}

public interface ISearchAgent
{
    /// <summary>
    /// Execute search plan and gather information, yielding sources as they're fetched
    /// </summary>
    IAsyncEnumerable<object> ExecuteSearchPlanAsync(ResearchPlan plan, int derpificationLevel = 100, CancellationToken cancellationToken = default);
}

public interface ISynthesisAgent
{
    /// <summary>
    /// Create comprehensive response from gathered info
    /// </summary>
    IAsyncEnumerable<string> SynthesizeAsync(
        string userQuery,
        ResearchPlan plan,
        GatheredInformation info,
        MemoryChunk[] relevantMemories,
        int derpificationLevel = 100,
        CancellationToken cancellationToken = default
    );
}

public interface IReflectionAgent
{
    /// <summary>
    /// Evaluate confidence in the synthesized response
    /// </summary>
    Task<ReflectionResult> ReflectAsync(
        string userQuery,
        string synthesizedResponse,
        GatheredInformation info,
        int derpificationLevel = 100,
        CancellationToken cancellationToken = default
    );
}

public interface IClarificationAgent
{
    /// <summary>
    /// Generate clarifying questions to better understand user's research intent
    /// </summary>
    Task<ClarificationResult> GenerateClarifyingQuestionsAsync(
        string userQuery,
        ConversationContext context,
        int derpificationLevel = 100,
        CancellationToken cancellationToken = default
    );
}
