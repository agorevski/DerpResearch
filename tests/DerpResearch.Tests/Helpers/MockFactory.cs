using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DerpResearch.Tests.Helpers;

/// <summary>
/// Factory for creating mock objects commonly used in tests
/// </summary>
public static class TestMockFactory
{
    public static Mock<ILogger<T>> CreateLogger<T>()
    {
        return new Mock<ILogger<T>>();
    }

    public static Mock<IConfiguration> CreateConfiguration(Dictionary<string, string>? settings = null)
    {
        var mock = new Mock<IConfiguration>();
        
        if (settings != null)
        {
            foreach (var setting in settings)
            {
                mock.Setup(c => c[setting.Key]).Returns(setting.Value);
            }
        }
        
        return mock;
    }

    public static Mock<ILLMService> CreateLLMService()
    {
        var mock = new Mock<ILLMService>();
        
        // Default behavior: return simple responses
        mock.Setup(s => s.ChatCompletion(
                It.IsAny<ChatMessage[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test response");
        
        mock.Setup(s => s.ChatCompletionStream(
                It.IsAny<ChatMessage[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new[] { "Test ", "streaming ", "response" }));
        
        mock.Setup(s => s.GetEmbedding(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateTestEmbedding());
        
        return mock;
    }

    public static Mock<IMemoryService> CreateMemoryService()
    {
        var mock = new Mock<IMemoryService>();
        
        mock.Setup(m => m.CreateConversationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-conversation-id");
        
        mock.Setup(m => m.GetConversationContextAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateConversationContext());
        
        mock.Setup(m => m.SaveMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        return mock;
    }

    public static Mock<ISearchService> CreateSearchService()
    {
        var mock = new Mock<ISearchService>();
        
        mock.Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateSearchResults(5).ToArray());
        
        return mock;
    }

    public static Mock<IWebContentFetcher> CreateWebContentFetcher()
    {
        var mock = new Mock<IWebContentFetcher>();
        
        mock.Setup(f => f.FetchContentAsync(
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> 
            { 
                ["https://example.com"] = "Test web content for the given URL." 
            });
        
        return mock;
    }

    public static Mock<IClarificationAgent> CreateClarificationAgent()
    {
        var mock = new Mock<IClarificationAgent>();
        
        mock.Setup(a => a.GenerateClarifyingQuestionsAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationContext>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateClarificationResult());
        
        return mock;
    }

    public static Mock<IPlannerAgent> CreatePlannerAgent()
    {
        var mock = new Mock<IPlannerAgent>();
        
        mock.Setup(a => a.CreatePlanAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationContext>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateResearchPlan());
        
        return mock;
    }

    public static Mock<ISearchAgent> CreateSearchAgent()
    {
        var mock = new Mock<ISearchAgent>();
        
        mock.Setup(a => a.ExecuteSearchPlanAsync(
                It.IsAny<ResearchPlan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new object[] 
            { 
                TestDataBuilder.CreateProgressUpdate("Searching...", "search"),
                TestDataBuilder.CreateSourceUpdate("Test Source", "https://example.com")
            }));
        
        return mock;
    }

    public static Mock<ISynthesisAgent> CreateSynthesisAgent()
    {
        var mock = new Mock<ISynthesisAgent>();
        
        mock.Setup(a => a.SynthesizeAsync(
                It.IsAny<string>(),
                It.IsAny<ResearchPlan>(),
                It.IsAny<GatheredInformation>(),
                It.IsAny<MemoryChunk[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new[] { "Synthesized ", "response ", "content" }));
        
        return mock;
    }

    public static Mock<IReflectionAgent> CreateReflectionAgent()
    {
        var mock = new Mock<IReflectionAgent>();
        
        mock.Setup(a => a.ReflectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GatheredInformation>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestDataBuilder.CreateReflectionResult());
        
        return mock;
    }

    public static Mock<IOrchestratorService> CreateOrchestratorService()
    {
        var mock = new Mock<IOrchestratorService>();
        
        mock.Setup(o => o.ProcessDeepResearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new[] 
            { 
                "Deep ", 
                "research ", 
                "response" 
            }));
        
        return mock;
    }

    /// <summary>
    /// Helper method to create an IAsyncEnumerable from a collection
    /// </summary>
    public static async IAsyncEnumerable<T> CreateAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await Task.Delay(1); // Simulate async behavior
            yield return item;
        }
    }

    /// <summary>
    /// Creates a mock that throws an exception
    /// </summary>
    public static Mock<T> CreateThrowingMock<T>(Exception exception) where T : class
    {
        var mock = new Mock<T>();
        
        // Setup all methods to throw
        mock.Setup(m => m.Equals(It.IsAny<object>()))
            .Throws(exception);
        
        return mock;
    }

    /// <summary>
    /// Creates a configuration mock with Azure OpenAI settings
    /// </summary>
    public static Mock<IConfiguration> CreateAzureOpenAIConfiguration(
        string endpoint = "https://test.openai.azure.com",
        string apiKey = "test-api-key",
        string chatModel = "gpt-4o",
        string miniModel = "gpt-4o-mini",
        string embeddingModel = "text-embedding-3-large")
    {
        var settings = new Dictionary<string, string>
        {
            ["AzureOpenAI:Endpoint"] = endpoint,
            ["AzureOpenAI:ApiKey"] = apiKey,
            ["AzureOpenAI:Deployments:Chat"] = chatModel,
            ["AzureOpenAI:Deployments:ChatMini"] = miniModel,
            ["AzureOpenAI:Deployments:Embedding"] = embeddingModel,
            ["Memory:DatabasePath"] = ":memory:",
            ["Reflection:ConfidenceThreshold"] = "0.7",
            ["Reflection:MaxIterations"] = "2"
        };
        
        return CreateConfiguration(settings);
    }
}
