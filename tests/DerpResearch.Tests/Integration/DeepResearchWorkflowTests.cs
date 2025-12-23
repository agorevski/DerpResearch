using System.Net;
using System.Net.Http.Json;
using DeepResearch.WebApp.Middleware;
using DeepResearch.WebApp.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace DerpResearch.Tests.Integration;

/// <summary>
/// Integration tests for end-to-end deep research workflows.
/// Tests HTTP endpoints, SSE streaming, and multi-agent coordination.
/// Fixes anti-pattern #16: Missing Integration Tests.
/// </summary>
[Collection("Integration")]
public class DeepResearchWorkflowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DeepResearchWorkflowTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Clear existing sources and add test configuration
                // This ensures mock services are used for integration tests
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UseMockServices"] = "true",
                    ["UseResilientServices"] = "false",
                    ["AzureOpenAI:Endpoint"] = "https://test.openai.azure.com",
                    ["AzureOpenAI:ApiKey"] = "test-api-key",
                    ["Memory:DatabasePath"] = "Data/test-integration.db"
                });
            });
        });
        
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateConversation_ReturnsNewConversationId()
    {
        // Act
        var response = await _client.PostAsync("/api/chat/new", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<CreateConversationResponse>();
        result.Should().NotBeNull();
        result!.ConversationId.Should().NotBeNullOrEmpty();
        Guid.TryParse(result.ConversationId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task GetHistory_NonExistentConversation_ReturnsEmptyMessages()
    {
        // Arrange
        var conversationId = Guid.NewGuid().ToString();

        // Act
        var response = await _client.GetAsync($"/api/chat/history/{conversationId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ConversationHistoryResponse>();
        result.Should().NotBeNull();
        result!.ConversationId.Should().Be(conversationId);
        result.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task CorrelationId_WhenNotProvided_IsGenerated()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.Headers.Should().ContainKey(CorrelationIdMiddleware.CorrelationIdHeaderName);
        var correlationId = response.Headers.GetValues(CorrelationIdMiddleware.CorrelationIdHeaderName).First();
        correlationId.Should().NotBeNullOrEmpty();
        Guid.TryParse(correlationId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task CorrelationId_WhenProvided_IsEchoed()
    {
        // Arrange
        var expectedCorrelationId = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(CorrelationIdMiddleware.CorrelationIdHeaderName, expectedCorrelationId);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.Headers.Should().ContainKey(CorrelationIdMiddleware.CorrelationIdHeaderName);
        var correlationId = response.Headers.GetValues(CorrelationIdMiddleware.CorrelationIdHeaderName).First();
        correlationId.Should().Be(expectedCorrelationId);
    }

    [Fact]
    public async Task Chat_WithMockServices_StreamsEvents()
    {
        // Arrange
        var request = new ChatRequest(
            Prompt: "What is machine learning?",
            Mode: "deep-research",
            ConversationId: null,
            DerpificationLevel: 100,
            ClarificationAnswers: null
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");
        
        // Read SSE stream
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("data:");
    }

    [Fact]
    public async Task Chat_WithClarificationAnswers_ProcessesCorrectly()
    {
        // Arrange - First create a conversation
        var createResponse = await _client.PostAsync("/api/chat/new", null);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateConversationResponse>();
        var conversationId = createResult!.ConversationId;

        var request = new ChatRequest(
            Prompt: "Tell me more about AI",
            Mode: "deep-research",
            ConversationId: conversationId,
            DerpificationLevel: 100,
            ClarificationAnswers: new[] { "Focus on healthcare applications" }
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // DTOs for response deserialization
    private record CreateConversationResponse(string ConversationId);
    private record ConversationHistoryResponse(string ConversationId, ChatMessage[] Messages);
}
