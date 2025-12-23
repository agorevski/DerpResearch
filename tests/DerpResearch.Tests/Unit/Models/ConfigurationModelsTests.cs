using DeepResearch.WebApp.Models;
using FluentAssertions;
using Xunit;

namespace DerpResearch.Tests.Unit.Models;

public class ConfigurationTests
{
    [Fact]
    public void ReflectionConfiguration_HasCorrectDefaults()
    {
        // Act
        var config = new ReflectionConfiguration();

        // Assert
        config.ConfidenceThreshold.Should().Be(0.7);
        config.MaxIterations.Should().Be(2);
        ReflectionConfiguration.Section.Should().Be("Reflection");
    }

    [Fact]
    public void MemoryConfiguration_HasCorrectDefaults()
    {
        // Act
        var config = new MemoryConfiguration();

        // Assert
        config.DatabasePath.Should().Be("Data/deepresearch.db");
        config.FaissIndexPath.Should().Be("Data/faiss.index");
        config.MaxMemoryAge.Should().Be(90);
        config.TopKResults.Should().Be(5);
        MemoryConfiguration.Section.Should().Be("Memory");
    }

    [Fact]
    public void AzureOpenAIConfiguration_HasCorrectDefaults()
    {
        // Act
        var config = new AzureOpenAIConfiguration();

        // Assert
        config.Endpoint.Should().BeEmpty();
        config.ApiKey.Should().BeEmpty();
        config.Deployments.Should().NotBeNull();
        AzureOpenAIConfiguration.Section.Should().Be("AzureOpenAI");
    }

    [Fact]
    public void DeploymentConfiguration_HasCorrectDefaults()
    {
        // Act
        var config = new DeploymentConfiguration();

        // Assert
        config.Chat.Should().Be("gpt-4o");
        config.ChatMini.Should().Be("gpt-4o-mini");
        config.Embedding.Should().Be("text-embedding-3-large");
    }

    [Fact]
    public void SearchConfiguration_HasCorrectDefaults()
    {
        // Act
        var config = new SearchConfiguration();

        // Assert
        config.CacheDuration.Should().Be(86400);
        config.MaxResults.Should().Be(10);
        SearchConfiguration.Section.Should().Be("Search");
    }

    [Fact]
    public void GoogleCustomSearchConfiguration_HasCorrectDefaults()
    {
        // Act
        var config = new GoogleCustomSearchConfiguration();

        // Assert
        config.ApiKey.Should().BeEmpty();
        config.SearchEngineId.Should().BeEmpty();
        GoogleCustomSearchConfiguration.Section.Should().Be("GoogleCustomSearch");
    }

    [Fact]
    public void MockServicesConfiguration_HasCorrectDefaults()
    {
        // Act
        var config = new MockServicesConfiguration();

        // Assert
        config.FixedConfidenceScore.Should().Be(0.95f);
        config.UseFixedConfidence.Should().BeFalse();
        MockServicesConfiguration.Section.Should().Be("MockServices");
    }

    [Fact]
    public void ResilienceConfiguration_HasCorrectDefaults()
    {
        // Act
        var config = new ResilienceConfiguration();

        // Assert
        config.FailureThreshold.Should().Be(5);
        config.BreakDurationSeconds.Should().Be(30);
        config.MaxRetryAttempts.Should().Be(3);
        config.MaxConcurrentRequests.Should().Be(2);
        config.RequestsPerSecond.Should().Be(1);
        config.LLMTimeoutSeconds.Should().Be(120);
        ResilienceConfiguration.Section.Should().Be("Resilience");
    }

    [Fact]
    public void ReflectionConfiguration_CanSetCustomValues()
    {
        // Act
        var config = new ReflectionConfiguration
        {
            ConfidenceThreshold = 0.8,
            MaxIterations = 5
        };

        // Assert
        config.ConfidenceThreshold.Should().Be(0.8);
        config.MaxIterations.Should().Be(5);
    }

    [Fact]
    public void MemoryConfiguration_CanSetCustomValues()
    {
        // Act
        var config = new MemoryConfiguration
        {
            DatabasePath = "custom/path.db",
            FaissIndexPath = "custom/index.faiss",
            MaxMemoryAge = 30,
            TopKResults = 10
        };

        // Assert
        config.DatabasePath.Should().Be("custom/path.db");
        config.FaissIndexPath.Should().Be("custom/index.faiss");
        config.MaxMemoryAge.Should().Be(30);
        config.TopKResults.Should().Be(10);
    }

    [Fact]
    public void AzureOpenAIConfiguration_CanSetCustomValues()
    {
        // Act
        var config = new AzureOpenAIConfiguration
        {
            Endpoint = "https://custom.openai.azure.com",
            ApiKey = "test-api-key",
            Deployments = new DeploymentConfiguration
            {
                Chat = "custom-chat",
                ChatMini = "custom-mini",
                Embedding = "custom-embedding"
            }
        };

        // Assert
        config.Endpoint.Should().Be("https://custom.openai.azure.com");
        config.ApiKey.Should().Be("test-api-key");
        config.Deployments.Chat.Should().Be("custom-chat");
        config.Deployments.ChatMini.Should().Be("custom-mini");
        config.Deployments.Embedding.Should().Be("custom-embedding");
    }
}
