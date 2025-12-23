using DeepResearch.WebApp.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace DerpResearch.Tests.Unit.Configuration;

public class ConfigurationTests
{
    [Fact]
    public void ReflectionConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange
        var config = new ReflectionConfiguration();

        // Assert
        config.ConfidenceThreshold.Should().Be(0.7);
        config.MaxIterations.Should().Be(2);
    }

    [Fact]
    public void MemoryConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange
        var config = new MemoryConfiguration();

        // Assert
        config.DatabasePath.Should().Be("Data/deepresearch.db");
        config.FaissIndexPath.Should().Be("Data/faiss.index");
        config.MaxMemoryAge.Should().Be(90);
        config.TopKResults.Should().Be(5);
    }

    [Fact]
    public void SearchConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange
        var config = new SearchConfiguration();

        // Assert
        config.CacheDuration.Should().Be(86400);
        config.MaxResults.Should().Be(10);
    }

    [Fact]
    public void ResilienceConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange
        var config = new ResilienceConfiguration();

        // Assert
        config.FailureThreshold.Should().Be(5);
        config.BreakDurationSeconds.Should().Be(30);
        config.MaxRetryAttempts.Should().Be(3);
        config.MaxConcurrentRequests.Should().Be(2);
        config.RequestsPerSecond.Should().Be(1);
        config.LLMTimeoutSeconds.Should().Be(120);
    }

    [Fact]
    public void AzureOpenAIConfiguration_DefaultDeployments_AreCorrect()
    {
        // Arrange
        var config = new AzureOpenAIConfiguration();

        // Assert
        config.Deployments.Chat.Should().Be("gpt-4o");
        config.Deployments.ChatMini.Should().Be("gpt-4o-mini");
        config.Deployments.Embedding.Should().Be("text-embedding-3-large");
    }

    [Fact]
    public void Configuration_CanBindFromDictionary()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Reflection:ConfidenceThreshold"] = "0.85",
            ["Reflection:MaxIterations"] = "5",
            ["Memory:DatabasePath"] = "test/test.db",
            ["Memory:TopKResults"] = "10"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.Configure<ReflectionConfiguration>(configuration.GetSection(ReflectionConfiguration.Section));
        services.Configure<MemoryConfiguration>(configuration.GetSection(MemoryConfiguration.Section));

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var reflectionConfig = serviceProvider.GetRequiredService<IOptions<ReflectionConfiguration>>().Value;
        var memoryConfig = serviceProvider.GetRequiredService<IOptions<MemoryConfiguration>>().Value;

        // Assert
        reflectionConfig.ConfidenceThreshold.Should().Be(0.85);
        reflectionConfig.MaxIterations.Should().Be(5);
        memoryConfig.DatabasePath.Should().Be("test/test.db");
        memoryConfig.TopKResults.Should().Be(10);
    }
}
