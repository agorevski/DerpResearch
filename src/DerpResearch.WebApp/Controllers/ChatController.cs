using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;

namespace DeepResearch.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("fixed")]
public class ChatController : ControllerBase
{
    private readonly IOrchestratorService _orchestrator;
    private readonly IMemoryService _memoryService;
    private readonly ILogger<ChatController> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ChatController(
        IOrchestratorService orchestrator,
        IMemoryService memoryService,
        ILogger<ChatController> logger)
    {
        _orchestrator = orchestrator;
        _memoryService = memoryService;
        _logger = logger;
    }

    [HttpPost]
    public async Task Chat([FromBody] ChatRequest request)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = 400;
            Response.ContentType = "application/json";
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            await Response.WriteAsJsonAsync(new { errors });
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        // Create or use existing conversation
        var conversationId = request.ConversationId ?? await _memoryService.CreateConversationAsync();

        _logger.LogInformation("Processing deep research request for conversation {ConversationId}", 
            conversationId);

        try
        {
            IAsyncEnumerable<string> stream = _orchestrator.ProcessDeepResearchAsync(
                request.Prompt, 
                conversationId, 
                request.DerpificationLevel, 
                request.ClarificationAnswers);

            await foreach (var token in stream)
            {
                // Check if token is already a JSON object (from structured updates)
                if (token.TrimStart().StartsWith("{"))
                {
                    // Already serialized - send as-is with proper SSE formatting
                    await Response.WriteAsync($"data: {token}\n\n");
                    await Response.Body.FlushAsync();
                }
                else
                {
                    // Plain text token - wrap in StreamToken
                    var eventData = new StreamToken(token, conversationId);
                    var json = JsonSerializer.Serialize(eventData, _jsonOptions);
                    
                    await Response.WriteAsync($"data: {json}\n\n");
                    await Response.Body.FlushAsync();
                }
            }

            // Send completion event
            var completionData = new StreamToken("", conversationId, "done");
            var completionJson = JsonSerializer.Serialize(completionData, _jsonOptions);
            await Response.WriteAsync($"data: {completionJson}\n\n");
            await Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            
            var errorData = new StreamToken($"Error: {ex.Message}", conversationId, "error");
            var errorJson = JsonSerializer.Serialize(errorData, _jsonOptions);
            await Response.WriteAsync($"data: {errorJson}\n\n");
            await Response.Body.FlushAsync();
        }
    }

    [HttpGet("history/{conversationId}")]
    public async Task<IActionResult> GetHistory(string conversationId)
    {
        try
        {
            var context = await _memoryService.GetConversationContextAsync(conversationId);
            return Ok(new
            {
                conversationId,
                messages = context.RecentMessages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation history");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("new")]
    public async Task<IActionResult> CreateConversation()
    {
        try
        {
            var conversationId = await _memoryService.CreateConversationAsync();
            return Ok(new { conversationId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating conversation");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
