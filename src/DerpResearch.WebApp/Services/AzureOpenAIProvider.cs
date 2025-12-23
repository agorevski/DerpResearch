using Azure;
using Azure.AI.OpenAI;
using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using Microsoft.Extensions.Options;
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
    private readonly AzureOpenAIConfiguration _config;
    private readonly ILogger<AzureOpenAIProvider> _logger;

    public string ProviderName => "AzureOpenAI";

    public AzureOpenAIProvider(IOptions<AzureOpenAIConfiguration> config, ILogger<AzureOpenAIProvider> logger)
    {
        _config = config.Value;
        _logger = logger;

        if (string.IsNullOrEmpty(_config.Endpoint))
        {
            throw new InvalidOperationException("AzureOpenAI:Endpoint configuration is missing.");
        }

        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            throw new InvalidOperationException("AzureOpenAI:ApiKey configuration is missing.");
        }

        var endpoint = new Uri(_config.Endpoint);
        _client = new AzureOpenAIClient(endpoint, new AzureKeyCredential(_config.ApiKey));

        _logger.LogInformation("AzureOpenAIProvider initialized with endpoint: {Endpoint}", _config.Endpoint);
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

        var deployment = _config.Deployments.Embedding;
        var embeddingClient = _client.GetEmbeddingClient(deployment);

        var response = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        return response.Value.ToFloats().ToArray();
    }

    private string ResolveDeployment(string modelName)
    {
        // Map known model names to deployments
        return modelName.ToLower() switch
        {
            "gpt-4o" => _config.Deployments.Chat,
            "gpt-4o-mini" => _config.Deployments.ChatMini,
            _ => modelName
        };
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
