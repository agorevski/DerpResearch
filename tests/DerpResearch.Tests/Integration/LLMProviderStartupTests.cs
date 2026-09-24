using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DerpResearch.Tests.Integration;

[Collection("Integration")]
public class LLMProviderStartupTests
{
    [Fact]
    public void ApplicationResolvesSelectedCompatibleProviderWithoutAzureCredentials()
    {
        var dbPath = Path.Combine("Data", $"provider-startup-{Guid.NewGuid():N}.db");
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("LLM:Provider", "OpenRouter");
                builder.UseSetting("OpenAICompatible:BaseUrl", "https://openrouter.ai/api/v1");
                builder.UseSetting("OpenAICompatible:ApiKey", "test-chat-key");
                builder.UseSetting("OpenAICompatible:Models:Chat", "vendor/chat");
                builder.UseSetting("OpenAICompatible:Models:ChatMini", "vendor/mini");
                builder.UseSetting("OpenAICompatible:Models:Embedding", "vendor/embed");
                builder.UseSetting("Memory:DatabasePath", dbPath);
            });

            factory.Services.GetRequiredService<ILLMProvider>()
                .Should().BeOfType<OpenAICompatibleProvider>();
        }
        finally
        {
            File.Delete(dbPath);
            File.Delete(dbPath + "-wal");
            File.Delete(dbPath + "-shm");
        }
    }
}
