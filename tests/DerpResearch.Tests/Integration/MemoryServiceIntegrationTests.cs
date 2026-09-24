using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DeepResearch.WebApp.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DerpResearch.Tests.Integration;

/// <summary>
/// Integration tests for MemoryService operations.
/// Tests actual database operations and end-to-end storage/retrieval workflows.
/// Fixes anti-pattern #16: Missing Integration Tests.
/// </summary>
[Collection("Integration")]
public class MemoryServiceIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private IMemoryService? _memoryService;
    private string _testConversationId = string.Empty;

    public MemoryServiceIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("UseMockServices", "true");
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Configure mock services for integration tests
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UseMockServices"] = "true",
                    ["UseResilientServices"] = "false",
                    ["AzureOpenAI:Endpoint"] = "https://test.openai.azure.com",
                    ["AzureOpenAI:ApiKey"] = "test-api-key",
                    ["Memory:DatabasePath"] = "Data/test-memory-integration.db"
                });
            });
        });
    }

    public async Task InitializeAsync()
    {
        // Get the memory service from the test server
        var scope = _factory.Services.CreateScope();
        _memoryService = scope.ServiceProvider.GetRequiredService<IMemoryService>();
        
        // Create a test conversation
        _testConversationId = await _memoryService.CreateConversationAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateConversation_ReturnsValidConversationId()
    {
        // Act
        var conversationId = await _memoryService!.CreateConversationAsync();

        // Assert
        conversationId.Should().NotBeNullOrEmpty();
        Guid.TryParse(conversationId, out _).Should().BeTrue("Conversation ID should be a valid GUID");
    }

    [Fact]
    public async Task SaveAndRetrieveMessage_RoundTrip_Success()
    {
        // Arrange
        var role = "user";
        var content = "What is the capital of France?";

        // Act
        await _memoryService!.SaveMessageAsync(_testConversationId, role, content);
        var context = await _memoryService.GetConversationContextAsync(_testConversationId);

        // Assert
        context.Should().NotBeNull();
        context.ConversationId.Should().Be(_testConversationId);
        context.RecentMessages.Should().ContainSingle(m => 
            m.Role == role && m.Content == content);
    }

    [Fact]
    public async Task GetConversationContext_MultipleMessages_ReturnsInOrder()
    {
        // Arrange
        var messages = new[]
        {
            ("user", "Question 1"),
            ("assistant", "Answer 1"),
            ("user", "Question 2"),
            ("assistant", "Answer 2")
        };

        // Act
        foreach (var (role, content) in messages)
        {
            await _memoryService!.SaveMessageAsync(_testConversationId, role, content);
            await Task.Delay(10); // Small delay to ensure ordering
        }

        var context = await _memoryService!.GetConversationContextAsync(_testConversationId);

        // Assert
        context.RecentMessages.Should().HaveCount(4);
        context.RecentMessages[0].Content.Should().Be("Question 1");
        context.RecentMessages[3].Content.Should().Be("Answer 2");
    }

    [Fact]
    public async Task StoreClarificationQuestions_RoundTrip_Success()
    {
        // Arrange
        var questions = new[] { "What time period?", "Which region specifically?" };

        // Act
        await _memoryService!.StoreClarificationQuestionsAsync(_testConversationId, questions);
        var retrieved = await _memoryService.GetClarificationQuestionsAsync(_testConversationId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Should().BeEquivalentTo(questions);
    }

    [Fact]
    public async Task ClearClarificationQuestions_RemovesQuestions()
    {
        // Arrange
        var questions = new[] { "Question 1?", "Question 2?" };
        await _memoryService!.StoreClarificationQuestionsAsync(_testConversationId, questions);

        // Act
        await _memoryService.ClearClarificationQuestionsAsync(_testConversationId);
        var retrieved = await _memoryService.GetClarificationQuestionsAsync(_testConversationId);

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetConversationContext_NonExistentConversation_ReturnsEmptyContext()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid().ToString();

        // Act
        var context = await _memoryService!.GetConversationContextAsync(nonExistentId);

        // Assert
        context.Should().NotBeNull();
        context.ConversationId.Should().Be(nonExistentId);
        context.RecentMessages.Should().BeEmpty();
        context.RelevantMemories.Should().BeEmpty();
    }
}

public class MemoryServiceEmbeddingDimensionTests : IDisposable
{
    private readonly string _databasePath = Path.Combine("Data", $"memory-dimension-{Guid.NewGuid():N}.db");
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });
    private readonly Mock<ILLMService> _llmService = new();

    public MemoryServiceEmbeddingDimensionTests()
    {
        _llmService.Setup(service => service.GetEmbedding(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([1f, 0f, 0f]);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
        File.Delete(_databasePath);
    }

    [Fact]
    public async Task InitializeAsync_RejectsPersistedEmbeddingsWithAnotherDimension()
    {
        var original = CreateService(3);
        await original.InitializeAsync();
        (await original.StoreMemoryAsync("Saved before switching models", "test", []))
            .IsFullySuccessful.Should().BeTrue();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(2).InitializeAsync());

        exception.Message.Should().Contain(_databasePath)
            .And.Contain("dimension 3")
            .And.Contain("Memory:EmbeddingDimension=2")
            .And.Contain("Memory:DatabasePath")
            .And.Contain("re-embed");

        var restored = CreateService(3);
        await restored.InitializeAsync();
        (await restored.SearchMemoryAsync("Saved before switching models"))
            .Should().ContainSingle().Which.Text.Should().Be("Saved before switching models");
    }

    [Fact]
    public async Task InitializeAsync_RejectsEmbeddingSizeMismatchEvenIfMetadataMatches()
    {
        var original = CreateService(3);
        await original.InitializeAsync();
        (await original.StoreMemoryAsync("Saved vector", "test", []))
            .IsFullySuccessful.Should().BeTrue();

        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE VectorStore SET Embedding = zeroblob(8)";
            await command.ExecuteNonQueryAsync();
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(3).InitializeAsync());

        exception.Message.Should().Contain("embedding size 8 bytes")
            .And.Contain("Memory:EmbeddingDimension=3 (12 bytes)");
    }

    [Fact]
    public async Task InitializeAsync_FreshDatabaseWithCustomDimensionRestoresAndAddsVectors()
    {
        var original = CreateService(3);
        await original.InitializeAsync();
        (await original.StoreMemoryAsync("First fact", "test", []))
            .IsFullySuccessful.Should().BeTrue();

        var restored = CreateService(3);
        await restored.InitializeAsync();
        (await restored.StoreMemoryAsync("Second fact", "test", []))
            .IsFullySuccessful.Should().BeTrue();

        (await restored.SearchMemoryAsync("fact", topK: 2))
            .Select(memory => memory.Text).Should().BeEquivalentTo(["First fact", "Second fact"]);
    }

    [Fact]
    public async Task InitializeAsync_CanceledBeforeDatabaseCheckDoesNotCreateDatabase()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService(3).InitializeAsync(cancellation.Token));
        File.Exists(_databasePath).Should().BeFalse();
    }

    private MemoryService CreateService(int dimension) =>
        new(Options.Create(new MemoryConfiguration
            {
                DatabasePath = _databasePath,
                EmbeddingDimension = dimension
            }),
            _llmService.Object,
            _loggerFactory.CreateLogger<MemoryService>(),
            _loggerFactory);
}
