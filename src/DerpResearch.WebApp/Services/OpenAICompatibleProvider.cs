using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using Microsoft.Extensions.Options;

namespace DeepResearch.WebApp.Services;

/// <summary>
/// OpenAI-compatible chat and embeddings APIs (including OpenRouter-style API roots).
/// </summary>
public sealed class OpenAICompatibleProvider : ILLMProvider
{
    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _clientFactory;
    private readonly OpenAICompatibleConfiguration _config;
    private readonly Uri _chatUrl;
    private readonly Uri _embeddingUrl;
    private readonly string _embeddingKey;

    public string ProviderName => "OpenAICompatible";

    public OpenAICompatibleProvider(
        IOptions<OpenAICompatibleConfiguration> config,
        IHttpClientFactory clientFactory)
    {
        _config = config.Value;
        _clientFactory = clientFactory;
        _chatUrl = ApiUrl(_config.BaseUrl, "chat/completions");
        _embeddingUrl = ApiUrl(
            string.IsNullOrEmpty(_config.Embeddings.BaseUrl) ? _config.BaseUrl : _config.Embeddings.BaseUrl,
            "embeddings");
        _embeddingKey = string.IsNullOrEmpty(_config.Embeddings.ApiKey)
            ? _config.ApiKey
            : _config.Embeddings.ApiKey;
    }

    public async Task<string> CompleteAsync(LLMRequest request, CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateClient(nameof(OpenAICompatibleProvider));
        using var httpRequest = CreateChatRequest(request, stream: false);
        using var response = await client.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        EnsureSuccess(response, "chat completion");

        using var document = await ReadJsonAsync(response.Content, "chat completion", cancellationToken);
        EnsureNoApiError(document.RootElement, "chat completion");
        if (!TryGetFirstChoice(document.RootElement, out var choice) ||
            !choice.TryGetProperty("message", out var message) ||
            message.ValueKind != JsonValueKind.Object ||
            !message.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.String)
        {
            throw InvalidResponse("chat completion");
        }
        return content.GetString()!;
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        LLMRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateClient(nameof(OpenAICompatibleProvider));
        using var httpRequest = CreateChatRequest(request, stream: true);
        using var response = await client.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        EnsureSuccess(response, "streaming chat completion");

        if (response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            using var errorDocument = await ReadJsonAsync(
                response.Content, "streaming chat completion", cancellationToken);
            EnsureNoApiError(errorDocument.RootElement, "streaming chat completion");
            throw InvalidResponse("streaming chat completion");
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(body);
        var data = new StringBuilder();
        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (line.Length == 0)
            {
                if (data.Length == 0) continue;
                var chunk = data.ToString();
                data.Clear();
                if (chunk == "[DONE]") yield break;
                var token = ParseStreamChunk(chunk);
                if (token is not null) yield return token;
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (data.Length > 0) data.Append('\n');
                var value = line.AsSpan(5);
                if (value.Length > 0 && value[0] == ' ') value = value[1..];
                data.Append(value);
            }
        }

        // A final SSE event need not end with an empty line.
        if (data.Length > 0 && data.ToString() != "[DONE]")
        {
            var token = ParseStreamChunk(data.ToString());
            if (token is not null) yield return token;
        }
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateClient(nameof(OpenAICompatibleProvider));
        using var httpRequest = CreateRequest(_embeddingUrl, _embeddingKey,
            new { model = _config.Models.Embedding, input = text });
        using var response = await client.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        EnsureSuccess(response, "embedding");

        using var document = await ReadJsonAsync(response.Content, "embedding", cancellationToken);
        EnsureNoApiError(document.RootElement, "embedding");
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0 ||
            data[0].ValueKind != JsonValueKind.Object ||
            !data[0].TryGetProperty("embedding", out var values) ||
            values.ValueKind != JsonValueKind.Array || values.GetArrayLength() == 0)
        {
            throw InvalidResponse("embedding");
        }

        var embedding = new float[values.GetArrayLength()];
        for (var i = 0; i < embedding.Length; i++)
        {
            if (values[i].ValueKind != JsonValueKind.Number ||
                !values[i].TryGetSingle(out embedding[i]) || !float.IsFinite(embedding[i]))
            {
                throw InvalidResponse("embedding");
            }
        }
        return embedding;
    }

    private HttpRequestMessage CreateChatRequest(LLMRequest request, bool stream)
    {
        var messages = request.Messages.Select(m => new
        {
            role = m.Role.ToLowerInvariant(),
            content = m.Content
        }).ToArray();
        var httpRequest = CreateRequest(_chatUrl, _config.ApiKey, new
        {
            model = ResolveModel(request.ModelName),
            messages,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream
        });
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(
            stream ? "text/event-stream" : "application/json"));
        return httpRequest;
    }

    private string ResolveModel(string modelName) => modelName switch
    {
        "gpt-4o" => _config.Models.Chat,
        "gpt-4o-mini" => _config.Models.ChatMini,
        _ => modelName
    };

    private static Uri ApiUrl(string baseUrl, string path) =>
        new(new Uri(baseUrl.TrimEnd('/') + "/"), path);

    private static HttpRequestMessage CreateRequest(Uri url, string key, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(payload, RequestJsonOptions));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return request;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            // Provider error bodies may echo credentials or user prompts; do not log or include them.
            throw new HttpRequestException(
                $"OpenAI-compatible {operation} failed with HTTP {(int)response.StatusCode}.",
                null, response.StatusCode);
        }
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpContent content, string operation, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            throw InvalidResponse(operation);
        }
    }

    private static string? ParseStreamChunk(string data)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(data);
        }
        catch (JsonException)
        {
            throw InvalidResponse("streaming chat completion");
        }

        using (document)
        {
            var root = document.RootElement;
            EnsureNoApiError(root, "streaming chat completion");
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array)
            {
                // Some providers send a separate usage-only chunk after the last token.
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("usage", out _))
                    return null;
                throw InvalidResponse("streaming chat completion");
            }

            if (choices.GetArrayLength() == 0) return null;
            var choice = choices[0];
            if (choice.ValueKind != JsonValueKind.Object ||
                !choice.TryGetProperty("delta", out var delta) ||
                delta.ValueKind != JsonValueKind.Object ||
                !delta.TryGetProperty("content", out var content) ||
                content.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (content.ValueKind != JsonValueKind.String)
                throw InvalidResponse("streaming chat completion");
            return content.GetString();
        }
    }

    private static bool TryGetFirstChoice(JsonElement root, out JsonElement choice)
    {
        choice = default;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            return false;
        choice = choices[0];
        return choice.ValueKind == JsonValueKind.Object;
    }

    private static void EnsureNoApiError(JsonElement root, string operation)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("error", out var error) &&
            error.ValueKind != JsonValueKind.Null)
        {
            throw new InvalidOperationException($"OpenAI-compatible {operation} returned an API error.");
        }
    }

    private static InvalidOperationException InvalidResponse(string operation) =>
        new($"OpenAI-compatible {operation} returned an invalid response.");
}
