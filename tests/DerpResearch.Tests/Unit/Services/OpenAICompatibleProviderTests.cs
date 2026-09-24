using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DeepResearch.WebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DerpResearch.Tests.Unit.Services;

public class LLMProviderRegistrationTests
{
    [Fact]
    public void DefaultSelectsAzureWithoutConstructingClient()
    {
        new LLMConfiguration().Provider.Should().Be("AzureOpenAI");
        var services = new ServiceCollection();
        services.AddSelectedLLMProvider(Settings(new Dictionary<string, string?>
        {
            ["AzureOpenAI:Endpoint"] = "https://example.openai.azure.com/",
            ["AzureOpenAI:ApiKey"] = "example-key"
        }), false);

        services.Single(s => s.ServiceType == typeof(ILLMProvider))
            .ImplementationType.Should().Be<AzureOpenAIProvider>();
    }

    [Theory]
    [InlineData("OpenAICompatible")]
    [InlineData("OpenRouter")]
    public void SelectsCompatibleProviderAndBindsModels(string provider)
    {
        var settings = CompatibleSettings();
        settings["LLM:Provider"] = provider;
        var services = new ServiceCollection();
        services.AddSelectedLLMProvider(Settings(settings), false);

        services.Single(s => s.ServiceType == typeof(ILLMProvider))
            .ImplementationType.Should().Be<OpenAICompatibleProvider>();
        using var container = services.BuildServiceProvider();
        container.GetRequiredService<IOptions<OpenAICompatibleConfiguration>>().Value
            .Models.Chat.Should().Be("vendor/chat");
    }

    [Fact]
    public void MockModeBypassesInvalidProviderAndAllCredentials()
    {
        var services = new ServiceCollection();
        services.AddSelectedLLMProvider(Settings(new Dictionary<string, string?>
        {
            ["LLM:Provider"] = "not-a-provider"
        }), true);

        services.Should().NotContain(s => s.ServiceType == typeof(ILLMProvider));
    }

    [Theory]
    [InlineData("LLM:Provider", "not-a-provider", "LLM:Provider")]
    [InlineData("OpenAICompatible:BaseUrl", "", "OpenAICompatible:BaseUrl")]
    [InlineData("OpenAICompatible:BaseUrl", "https://api.example.test/v1?key=secret-value", "OpenAICompatible:BaseUrl")]
    [InlineData("OpenAICompatible:ApiKey", "", "OpenAICompatible:ApiKey")]
    [InlineData("OpenAICompatible:Models:Chat", "", "OpenAICompatible:Models:Chat")]
    [InlineData("OpenAICompatible:Models:ChatMini", "", "OpenAICompatible:Models:ChatMini")]
    [InlineData("OpenAICompatible:Models:Embedding", "", "OpenAICompatible:Models:Embedding")]
    [InlineData("OpenAICompatible:Embeddings:BaseUrl", "not-a-url", "OpenAICompatible:Embeddings:BaseUrl")]
    [InlineData("OpenAICompatible:Embeddings:ApiKey", "bad\nkey", "OpenAICompatible:Embeddings:ApiKey")]
    public void RejectsInvalidSelectedProviderConfigurationWithoutRevealingSecrets(
        string setting, string value, string expectedSetting)
    {
        var settings = CompatibleSettings();
        settings[setting] = value;
        var action = () => new ServiceCollection().AddSelectedLLMProvider(Settings(settings), false);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{expectedSetting}*")
            .And.Message.Should().NotContain("secret-value");
    }

    [Theory]
    [InlineData("AzureOpenAI:Endpoint", "invalid", "AzureOpenAI:Endpoint")]
    [InlineData("AzureOpenAI:ApiKey", "", "AzureOpenAI:ApiKey")]
    public void RejectsInvalidDefaultAzureConfiguration(string setting, string value, string expectedSetting)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AzureOpenAI:Endpoint"] = "https://example.openai.azure.com",
            ["AzureOpenAI:ApiKey"] = "example-key"
        };
        settings[setting] = value;
        var action = () => new ServiceCollection().AddSelectedLLMProvider(Settings(settings), false);

        action.Should().Throw<InvalidOperationException>().WithMessage($"*{expectedSetting}*");
    }

    [Fact]
    public void EnvironmentVariablesBindProviderAndSlashQualifiedModel()
    {
        var prefix = $"DERP_TEST_{Guid.NewGuid():N}__";
        var keys = new Dictionary<string, string>
        {
            [$"{prefix}LLM__Provider"] = "OpenRouter",
            [$"{prefix}OpenAICompatible__BaseUrl"] = "https://openrouter.ai/api/v1",
            [$"{prefix}OpenAICompatible__ApiKey"] = "example-key",
            [$"{prefix}OpenAICompatible__Models__Chat"] = "vendor/chat",
            [$"{prefix}OpenAICompatible__Models__ChatMini"] = "vendor/mini",
            [$"{prefix}OpenAICompatible__Models__Embedding"] = "vendor/embed"
        };
        try
        {
            foreach (var (key, value) in keys) Environment.SetEnvironmentVariable(key, value);
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build();
            var services = new ServiceCollection().AddSelectedLLMProvider(configuration, false);
            using var container = services.BuildServiceProvider();

            container.GetRequiredService<IOptions<OpenAICompatibleConfiguration>>().Value
                .Models.Chat.Should().Be("vendor/chat");
            services.Single(s => s.ServiceType == typeof(ILLMProvider))
                .ImplementationType.Should().Be<OpenAICompatibleProvider>();
        }
        finally
        {
            foreach (var key in keys.Keys) Environment.SetEnvironmentVariable(key, null);
        }
    }

    private static IConfiguration Settings(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Dictionary<string, string?> CompatibleSettings() => new()
    {
        ["LLM:Provider"] = "OpenAICompatible",
        ["OpenAICompatible:BaseUrl"] = "https://api.example.test/api/v1",
        ["OpenAICompatible:ApiKey"] = "example-key",
        ["OpenAICompatible:Models:Chat"] = "vendor/chat",
        ["OpenAICompatible:Models:ChatMini"] = "vendor/mini",
        ["OpenAICompatible:Models:Embedding"] = "vendor/embed"
    };
}

public class OpenAICompatibleProviderTests
{
    private static readonly LLMRequest Request = new()
    {
        Messages =
        [
            new ChatMessage { Role = "system", Content = "Be helpful" },
            new ChatMessage { Role = "user", Content = "Hi" }
        ],
        Temperature = 0.4f,
        MaxTokens = 23
    };

    [Theory]
    [InlineData("gpt-4o", "vendor/chat")]
    [InlineData("gpt-4o-mini", "vendor/mini")]
    [InlineData("other/model", "other/model")]
    public async Task ChatCompletionUsesCorrectUrlCredentialsModelAndResponse(string requested, string expected)
    {
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(JsonResponse("""{"choices":[{"message":{"content":"Hello!"}}]}""")));
        var provider = CreateProvider(handler);

        var result = await provider.CompleteAsync(Request with { ModelName = requested });

        result.Should().Be("Hello!");
        var sent = handler.OnlyRequest;
        sent.Url.Should().Be("https://openrouter.ai/api/v1/chat/completions");
        sent.Authorization.Should().Be("Bearer chat-key");
        sent.Method.Should().Be(HttpMethod.Post);
        using var json = JsonDocument.Parse(sent.Body);
        json.RootElement.GetProperty("model").GetString().Should().Be(expected);
        json.RootElement.GetProperty("messages")[0].GetProperty("role").GetString().Should().Be("system");
        json.RootElement.GetProperty("messages")[1].GetProperty("content").GetString().Should().Be("Hi");
        json.RootElement.GetProperty("max_tokens").GetInt32().Should().Be(23);
        json.RootElement.GetProperty("temperature").GetSingle().Should().BeApproximately(0.4f, 0.001f);
        json.RootElement.GetProperty("stream").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task StreamingParsesMixedSseFramesAndStopsAtDone()
    {
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    ": keepalive\r\n" +
                    "event: message\r\ndata: {\"choices\":[{\"delta\":{\"role\":\"assistant\"}}]}\r\n\r\n" +
                    "data: {\"choices\":[{\"delta\":{\"content\":\"Hel\"}}]}\r\n\r\n" +
                    "data: {\"choices\":[\r\ndata: {\"delta\":{\"content\":\"lo\"}}]}\r\n\r\n" +
                    "data: {\"choices\":[],\"usage\":{\"total_tokens\":3}}\r\n\r\n" +
                    "data: [DONE]\r\n\r\n" +
                    "data: {\"error\":{\"message\":\"unreachable\"}}\r\n\r\n",
                    Encoding.UTF8, "text/event-stream")
            }));
        var provider = CreateProvider(handler);

        var tokens = await provider.StreamCompletionAsync(Request with { ModelName = "gpt-4o-mini" })
            .ToArrayAsync();

        tokens.Should().Equal("Hel", "lo");
        handler.OnlyRequest.Url.Should().Be("https://openrouter.ai/api/v1/chat/completions");
        handler.OnlyRequest.Authorization.Should().Be("Bearer chat-key");
        using var json = JsonDocument.Parse(handler.OnlyRequest.Body);
        json.RootElement.GetProperty("model").GetString().Should().Be("vendor/mini");
        json.RootElement.GetProperty("stream").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task StreamingAcceptsFinalFrameWithoutTrailingBlankLine()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "data: {\"choices\":[{\"delta\":{\"content\":\"last\"}}]}",
                    Encoding.UTF8, "text/event-stream")
            }));

        var result = await CreateProvider(handler).StreamCompletionAsync(Request).ToArrayAsync();

        result.Should().Equal("last");
    }

    [Fact]
    public async Task StreamingSurfacesSseErrorWithoutRevealingBody()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "event: error\ndata: {\"error\":{\"message\":\"chat-key was rejected\"}}\n\n",
                    Encoding.UTF8, "text/event-stream")
            }));
        var action = async () => await CreateProvider(handler).StreamCompletionAsync(Request).ToArrayAsync();

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("API error").And.NotContain("chat-key");
    }

    [Fact]
    public async Task StreamingCancellationInterruptsAnOpenSseBody()
    {
        var pipe = new Pipe();
        var handler = new RecordingHandler((_, _) =>
        {
            var content = new StreamContent(pipe.Reader.AsStream());
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var action = async () => await CreateProvider(handler)
            .StreamCompletionAsync(Request, cancellation.Token).ToArrayAsync(cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        handler.Requests.Should().ContainSingle();
        await pipe.Writer.CompleteAsync();
    }

    [Theory]
    [InlineData(false, "https://openrouter.ai/api/v1/embeddings", "Bearer chat-key")]
    [InlineData(true, "https://embedding.example.test/v1/embeddings", "Bearer embedding-key")]
    public async Task EmbeddingSupportsSharedOrSeparateApi(bool separate, string url, string auth)
    {
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(JsonResponse("""{"data":[{"index":0,"embedding":[0.5,-1.25,2]}]}""")));
        var provider = CreateProvider(handler, separate);

        var result = await provider.GetEmbeddingAsync("some text");

        result.Should().Equal(0.5f, -1.25f, 2f);
        var sent = handler.OnlyRequest;
        sent.Url.Should().Be(url);
        sent.Authorization.Should().Be(auth);
        using var json = JsonDocument.Parse(sent.Body);
        json.RootElement.GetProperty("model").GetString().Should().Be("vendor/embed");
        json.RootElement.GetProperty("input").GetString().Should().Be("some text");
    }

    [Theory]
    [InlineData("chat")]
    [InlineData("stream")]
    [InlineData("embedding")]
    public async Task HttpFailuresReportStatusWithoutEchoingProviderBodyOrKey(string operation)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":{"message":"chat-key is invalid"}}""")
            }));
        var provider = CreateProvider(handler);

        var action = async () => await Invoke(provider, operation);

        var exception = await action.Should().ThrowAsync<HttpRequestException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        exception.Which.Message.Should().Contain("HTTP 401").And.NotContain("chat-key");
    }

    [Theory]
    [InlineData("chat")]
    [InlineData("stream")]
    [InlineData("embedding")]
    public async Task ApiErrorsAndInvalidResponsesAreNotSilentlyAccepted(string operation)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(
            """{"error":{"message":"chat-key is invalid"}}""")));
        var provider = CreateProvider(handler);
        var action = async () => await Invoke(provider, operation);

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("API error").And.NotContain("chat-key");
    }

    [Theory]
    [InlineData("chat")]
    [InlineData("stream")]
    [InlineData("embedding")]
    public async Task ForwardsCancellationToHttpTransport(string operation)
    {
        var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse("{}");
        });
        var provider = CreateProvider(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

        var action = async () => await Invoke(provider, operation, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        handler.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData("chat", "not json")]
    [InlineData("embedding", """{"data":[{"embedding":["not a number"]}]}""")]
    [InlineData("stream", "data: not json\n\n")]
    public async Task MalformedResultsFailWithoutEchoingResponse(string operation, string body)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(body)));
        var provider = CreateProvider(handler);
        var action = async () => await Invoke(provider, operation);

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("invalid response").And.NotContain(body);
    }

    [Fact]
    public async Task CompatibleEmbeddingsWorkThroughLlmAndMemoryServicesWithConfiguredDimension()
    {
        var folder = Path.Combine("Data", $"provider-test-{Guid.NewGuid():N}");
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(JsonResponse("""{"data":[{"embedding":[0.5,-1.25,2]}]}""")));
        try
        {
            using var loggerFactory = LoggerFactory.Create(_ => { });
            var llm = new LLMService(CreateProvider(handler), loggerFactory.CreateLogger<LLMService>());
            var memory = new MemoryService(
                Options.Create(new MemoryConfiguration
                {
                    DatabasePath = Path.Combine(folder, "memory.db"),
                    EmbeddingDimension = 3
                }),
                llm, loggerFactory.CreateLogger<MemoryService>(), loggerFactory);
            await memory.InitializeAsync();

            var stored = await memory.StoreMemoryAsync("A short fact", "test", []);
            var found = await memory.SearchMemoryAsync("fact");

            stored.IsFullySuccessful.Should().BeTrue();
            found.Should().ContainSingle().Which.Text.Should().Be("A short fact");
            handler.Requests.Should().HaveCount(2);
            handler.Requests.Should().OnlyContain(r =>
                r.Url == "https://openrouter.ai/api/v1/embeddings");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task Invoke(OpenAICompatibleProvider provider, string operation, CancellationToken ct = default)
    {
        switch (operation)
        {
            case "chat":
                await provider.CompleteAsync(Request, ct);
                break;
            case "stream":
                await provider.StreamCompletionAsync(Request, ct).ToArrayAsync(ct);
                break;
            default:
                await provider.GetEmbeddingAsync("text", ct);
                break;
        }
    }

    private static HttpResponseMessage JsonResponse(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static OpenAICompatibleProvider CreateProvider(RecordingHandler handler, bool separateEmbedding = false)
    {
        var config = new OpenAICompatibleConfiguration
        {
            BaseUrl = "https://openrouter.ai/api/v1",
            ApiKey = "chat-key",
            Models = new OpenAICompatibleModels
            {
                Chat = "vendor/chat",
                ChatMini = "vendor/mini",
                Embedding = "vendor/embed"
            },
            Embeddings = separateEmbedding
                ? new OpenAICompatibleEmbeddings
                {
                    BaseUrl = "https://embedding.example.test/v1/",
                    ApiKey = "embedding-key"
                }
                : new OpenAICompatibleEmbeddings()
        };
        return new OpenAICompatibleProvider(Options.Create(config), new RecordingClientFactory(handler));
    }

    private sealed class RecordingClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed record SentRequest(HttpMethod Method, string Url, string? Authorization, string Body);

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];
        public SentRequest OnlyRequest => Requests.Should().ContainSingle().Which;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new SentRequest(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(),
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            return await responder(request, cancellationToken);
        }
    }
}
