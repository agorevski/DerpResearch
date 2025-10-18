using Azure;
using Azure.AI.OpenAI;
using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenAI.Chat;
using OpenAI.Embeddings;
using ChatMessage = DeepResearch.WebApp.Models.ChatMessage;

namespace DeepResearch.WebApp.Services;

public class LLMService : ILLMService
{
    private readonly AzureOpenAIClient _client;
    private readonly IConfiguration _config;
    private readonly ILogger<LLMService> _logger;

    public LLMService(IConfiguration config, ILogger<LLMService> logger)
    {
        _config = config;
        _logger = logger;

        try
        {
            _logger.LogInformation("LLMService constructor starting...");
            
            var endpointStr = _config["AzureOpenAI:Endpoint"];
            var apiKey = _config["AzureOpenAI:ApiKey"];

            _logger.LogInformation("Configuration check - Endpoint present: {HasEndpoint}, ApiKey present: {HasApiKey}",
                !string.IsNullOrEmpty(endpointStr), !string.IsNullOrEmpty(apiKey));

            if (string.IsNullOrEmpty(endpointStr))
            {
                _logger.LogError("AzureOpenAI:Endpoint configuration is missing or empty");
                throw new InvalidOperationException("AzureOpenAI:Endpoint configuration is missing. Please set this in appsettings.Production.json or as an environment variable.");
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("AzureOpenAI:ApiKey configuration is missing or empty");
                throw new InvalidOperationException("AzureOpenAI:ApiKey configuration is missing. Please set this in appsettings.Production.json or as an environment variable.");
            }

            _logger.LogInformation("Creating Azure OpenAI client with endpoint: {Endpoint}", endpointStr);
            var endpoint = new Uri(endpointStr);
            _client = new AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey));
            
            _logger.LogInformation("LLMService initialized successfully with endpoint: {Endpoint}", endpointStr);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to initialize LLMService: {Message}. Type: {Type}", 
                ex.Message, ex.GetType().Name);
            _logger.LogCritical("Stack trace: {StackTrace}", ex.StackTrace);
            throw;
        }
    }

    public async IAsyncEnumerable<string> ChatCompletionStream(
        ChatMessage[] messages,
        string deploymentName = "gpt-4o")
    {
        var deployment = _config[$"AzureOpenAI:Deployments:{deploymentName}"] ?? deploymentName;
        var chatClient = _client.GetChatClient(deployment);

        var chatMessages = messages.Select(m => 
            m.Role.ToLower() switch
            {
                "system" => new SystemChatMessage(m.Content) as OpenAI.Chat.ChatMessage,
                "user" => new UserChatMessage(m.Content) as OpenAI.Chat.ChatMessage,
                "assistant" => new AssistantChatMessage(m.Content) as OpenAI.Chat.ChatMessage,
                _ => throw new ArgumentException($"Unknown role: {m.Role}")
            }
        ).ToList();

        await foreach (var update in chatClient.CompleteChatStreamingAsync(chatMessages))
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                yield return contentPart.Text;
            }
        }
    }

    public async Task<string> ChatCompletion(
        ChatMessage[] messages,
        string deploymentName = "gpt-4o")
    {
        var deployment = _config[$"AzureOpenAI:Deployments:{deploymentName}"] ?? deploymentName;
        var chatClient = _client.GetChatClient(deployment);

        var chatMessages = messages.Select(m =>
            m.Role.ToLower() switch
            {
                "system" => new SystemChatMessage(m.Content) as OpenAI.Chat.ChatMessage,
                "user" => new UserChatMessage(m.Content) as OpenAI.Chat.ChatMessage,
                "assistant" => new AssistantChatMessage(m.Content) as OpenAI.Chat.ChatMessage,
                _ => throw new ArgumentException($"Unknown role: {m.Role}")
            }
        ).ToList();

        var response = await chatClient.CompleteChatAsync(chatMessages);
        return response.Value.Content[0].Text;
    }

    public async Task<float[]> GetEmbedding(string text)
    {
        var deployment = _config["AzureOpenAI:Deployments:Embedding"] ?? "text-embedding-3-large";
        var embeddingClient = _client.GetEmbeddingClient(deployment);

        var response = await embeddingClient.GenerateEmbeddingAsync(text);
        return response.Value.ToFloats().ToArray();
    }

    public async Task<T?> GetStructuredOutput<T>(
        string prompt,
        string deploymentName = "gpt-4o") where T : class
    {
        var messages = new[]
        {
            new ChatMessage { Role = "system", Content = "You are a helpful assistant that responds in JSON format." },
            new ChatMessage { Role = "user", Content = prompt }
        };

        var response = await ChatCompletion(messages, deploymentName);

        try
        {
            // Extract JSON from markdown code blocks if present
            var jsonText = response.Trim();
            if (jsonText.StartsWith("```json"))
            {
                jsonText = jsonText.Substring(7);
                if (jsonText.EndsWith("```"))
                {
                    jsonText = jsonText.Substring(0, jsonText.Length - 3);
                }
            }
            else if (jsonText.StartsWith("```"))
            {
                jsonText = jsonText.Substring(3);
                if (jsonText.EndsWith("```"))
                {
                    jsonText = jsonText.Substring(0, jsonText.Length - 3);
                }
            }

            return JsonSerializer.Deserialize<T>(jsonText.Trim(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response: {Response}", response);
            return null;
        }
    }
}
