using System.Net;
using System.Net.Http.Json;
using DeepResearch.WebApp.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DerpResearch.Tests.Integration;

[Collection("Integration")]
public class ChatControllerTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ChatControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("UseMockServices", "true");
            builder.UseSetting("UseResilientServices", "false");
            builder.UseSetting("Memory:DatabasePath", "Data/test-integration.db");
        });
        _client = _factory.CreateClient();
    }

    [Theory]
    [InlineData("", 100, "Prompt")]
    [InlineData("Valid prompt", -1, "DerpificationLevel")]
    [InlineData("Valid prompt", 101, "DerpificationLevel")]
    public async Task Chat_InvalidInput_ReturnsBadRequest(string prompt, int level, string field)
    {
        using var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest(prompt, DerpificationLevel: level));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey(field);
    }

    [Fact]
    public async Task Chat_OverlengthPrompt_ReturnsBadRequest()
    {
        using var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest(new string('x', 10001)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey("Prompt");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task Chat_ValidInput_StreamsCompletion(int level)
    {
        using var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest("What is machine learning?", DerpificationLevel: level));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");
        var stream = await response.Content.ReadAsStringAsync();
        stream.Should().Contain("data: ");
        stream.Should().Contain("\"type\":\"done\"");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
