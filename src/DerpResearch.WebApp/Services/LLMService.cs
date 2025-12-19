using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ChatMessage = DeepResearch.WebApp.Models.ChatMessage;

namespace DeepResearch.WebApp.Services;

/// <summary>
/// LLM service that delegates to a provider abstraction.
/// Allows swapping between Azure OpenAI, OpenAI, Anthropic, or other providers.
/// </summary>
public class LLMService : ILLMService
{
    private readonly ILLMProvider _provider;
    private readonly ILogger<LLMService> _logger;

    public LLMService(ILLMProvider provider, ILogger<LLMService> logger)
    {
        _provider = provider;
        _logger = logger;

        _logger.LogInformation("LLMService initialized with provider: {Provider}", _provider.ProviderName);
    }

    public async IAsyncEnumerable<string> ChatCompletionStream(
        ChatMessage[] messages,
        string deploymentName = "gpt-4o",
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new LLMRequest
        {
            Messages = messages,
            ModelName = deploymentName
        };

        await foreach (var token in _provider.StreamCompletionAsync(request, cancellationToken))
        {
            yield return token;
        }
    }

    public async Task<string> ChatCompletion(
        ChatMessage[] messages,
        string deploymentName = "gpt-4o",
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("ChatCompletion - Model: {Model}, Provider: {Provider}",
            deploymentName, _provider.ProviderName);

        var request = new LLMRequest
        {
            Messages = messages,
            ModelName = deploymentName
        };

        return await _provider.CompleteAsync(request, cancellationToken);
    }

    public async Task<float[]> GetEmbedding(string text, CancellationToken cancellationToken = default)
    {
        return await _provider.GetEmbeddingAsync(text, cancellationToken);
    }

    public async Task<T?> GetStructuredOutput<T>(
        string prompt,
        string deploymentName = "gpt-4o",
        CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var messages = new[]
        {
            new ChatMessage { Role = "system", Content = "You are a helpful assistant that responds in JSON format." },
            new ChatMessage { Role = "user", Content = prompt }
        };

        var response = await ChatCompletion(messages, deploymentName, cancellationToken);

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
