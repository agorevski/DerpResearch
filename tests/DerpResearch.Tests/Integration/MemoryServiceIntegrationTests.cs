using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
