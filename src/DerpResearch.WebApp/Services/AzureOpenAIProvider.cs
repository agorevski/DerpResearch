using Azure;
using Azure.AI.OpenAI;
using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using OpenAI.Chat;
using OpenAI.Embeddings;
using System.Runtime.CompilerServices;
using ChatMessage = DeepResearch.WebApp.Models.ChatMessage;

namespace DeepResearch.WebApp.Services;

/// <summary>
/// Azure OpenAI implementation of ILLMProvider
/// </summary>
public class AzureOpenAIProvider : ILLMProvider
{
    private readonly AzureOpenAIClient _client;
    private readonly IConfiguration _config;
    private readonly ILogger<AzureOpenAIProvider> _logger;

    public string ProviderName => "AzureOpenAI";

    public AzureOpenAIProvider(IConfiguration config, ILogger<AzureOpenAIProvider> logger)
    {
        _config = config;
        _logger = logger;

        var endpointStr = _config["AzureOpenAI:Endpoint"];
        var apiKey = _config["AzureOpenAI:ApiKey"];

        if (string.IsNullOrEmpty(endpointStr))
        {
            throw new InvalidOperationException("AzureOpenAI:Endpoint configuration is missing.");
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("AzureOpenAI:ApiKey configuration is missing.");
        }

        var endpoint = new Uri(endpointStr);
        _client = new AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey));

        _logger.LogInformation("AzureOpenAIProvider initialized with endpoint: {Endpoint}", endpointStr);
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        LLMRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deployment = ResolveDeployment(request.ModelName);
        var chatClient = _client.GetChatClient(deployment);

        var chatMessages = ConvertMessages(request.Messages);

        await foreach (var update in chatClient.CompleteChatStreamingAsync(chatMessages, cancellationToken: cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var contentPart in update.ContentUpdate)
            {
                yield return contentPart.Text;
            }
        }
    }

    public async Task<string> CompleteAsync(
        LLMRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deployment = ResolveDeployment(request.ModelName);

        _logger.LogDebug("CompleteAsync - Model: {Model}, Deployment: {Deployment}",
            request.ModelName, deployment);

        var chatClient = _client.GetChatClient(deployment);
        var chatMessages = ConvertMessages(request.Messages);

        var response = await chatClient.CompleteChatAsync(chatMessages, cancellationToken: cancellationToken);
        return response.Value.Content[0].Text;
    }

    public async Task<float[]> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deployment = _config["AzureOpenAI:Deployments:Embedding"] ?? "text-embedding-3-large";
        var embeddingClient = _client.GetEmbeddingClient(deployment);

        var response = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        return response.Value.ToFloats().ToArray();
    }

    private string ResolveDeployment(string modelName)
    {
        return _config[$"AzureOpenAI:Deployments:{modelName}"] ?? modelName;
    }

    private static List<OpenAI.Chat.ChatMessage> ConvertMessages(ChatMessage[] messages)
    {
        return messages.Select(m =>
            m.Role.ToLower() switch
            {
                "system" => new SystemChatMessage(m.Content) as OpenAI.Chat.ChatMessage,
                "user" => new UserChatMessage(m.Content) as OpenAI.Chat.ChatMessage,
                "assistant" => new AssistantChatMessage(m.Content) as OpenAI.Chat.ChatMessage,
                _ => throw new ArgumentException($"Unknown role: {m.Role}")
            }
        ).ToList();
    }
}
