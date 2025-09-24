using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StellarAnvil.Api.Observability;
using StellarAnvil.Application.DTOs.OpenAI;
using StellarAnvil.Application.Services;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace StellarAnvil.Api.Controllers.OpenAI;

[ApiController]
[Route("v1")]
[Authorize(Policy = "OpenApiOnly")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Create a chat completion (OpenAI compatible)
    /// </summary>
    [HttpPost("chat/completions")]
    public async Task<IActionResult> CreateChatCompletion([FromBody] ChatCompletionRequest request)
    {
        using var activity = ActivitySources.AI.StartAIActivity("ChatCompletion", request.Model);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            if (request.Messages == null || !request.Messages.Any())
            {
                _logger.LogWarning("Chat completion request received with no messages");
                return BadRequest(new { error = new { message = "Messages are required" } });
            }

            _logger.LogInformation("Processing chat completion request with {MessageCount} messages using model {Model} (streaming: {Streaming})", 
                request.Messages.Count, request.Model, request.Stream);

            activity?.SetTag("ai.request.message_count", request.Messages.Count);
            activity?.SetTag("ai.request.temperature", request.Temperature);
            activity?.SetTag("ai.request.max_tokens", request.MaxTokens);
            activity?.SetTag("ai.request.stream", request.Stream);

            if (request.Stream)
            {
                return await ProcessStreamingRequest(request, activity, stopwatch);
            }
            else
            {
                return await ProcessNonStreamingRequest(request, activity, stopwatch);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to process chat completion request");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            return StatusCode(500, new { error = new { message = ex.Message } });
        }
    }

    private async Task<IActionResult> ProcessNonStreamingRequest(
        ChatCompletionRequest request, 
        Activity? activity, 
        Stopwatch stopwatch)
    {
        // Collect all chunks from the async enumerable
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in _chatService.ProcessChatCompletionAsync(request))
        {
            chunks.Add(chunk);
        }
        
        // Convert chunks to a single response
        var combinedContent = string.Join("", chunks
            .SelectMany(c => c.Choices)
            .Where(c => c.Delta.Content != null)
            .Select(c => c.Delta.Content));
        
        var lastChunk = chunks.LastOrDefault();
        var response = new ChatCompletionResponse
        {
            Id = lastChunk?.Id ?? $"chatcmpl-{Guid.NewGuid():N}",
            Object = "chat.completion",
            Created = lastChunk?.Created ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = request.Model,
            Choices = new List<Choice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessage
                    {
                        Role = "assistant",
                        Content = combinedContent
                    },
                    FinishReason = lastChunk?.Choices?.FirstOrDefault()?.FinishReason ?? "stop"
                }
            },
            Usage = lastChunk?.Usage ?? new Usage()
        };
        
        stopwatch.Stop();
        Metrics.ChatCompletions.Add(1, new KeyValuePair<string, object?>("model", request.Model));
        Metrics.ChatCompletionDuration.Record(stopwatch.Elapsed.TotalSeconds, 
            new KeyValuePair<string, object?>("model", request.Model));

        activity?.SetTag("ai.response.finish_reason", response.Choices.FirstOrDefault()?.FinishReason);
        activity?.SetTag("ai.response.usage.total_tokens", response.Usage?.TotalTokens);

        _logger.LogInformation("Chat completion processed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        
        return Ok(response);
    }

    private async Task<IActionResult> ProcessStreamingRequest(
        ChatCompletionRequest request, 
        Activity? activity, 
        Stopwatch stopwatch)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var responseStream = Response.Body;
        var writer = new StreamWriter(responseStream, Encoding.UTF8, leaveOpen: true);

        try
        {
            await foreach (var chunk in _chatService.ProcessChatCompletionAsync(request))
            {
                var json = JsonSerializer.Serialize(chunk, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });
                
                await writer.WriteLineAsync($"data: {json}");
                await writer.FlushAsync();
            }

            // Send the final [DONE] message
            await writer.WriteLineAsync("data: [DONE]");
            await writer.FlushAsync();

            stopwatch.Stop();
            Metrics.ChatCompletions.Add(1, new KeyValuePair<string, object?>("model", request.Model));
            Metrics.ChatCompletionDuration.Record(stopwatch.Elapsed.TotalSeconds, 
                new KeyValuePair<string, object?>("model", request.Model));

            _logger.LogInformation("Streaming chat completion processed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during streaming response");
            await writer.WriteLineAsync($"data: {{\"error\": {{\"message\": \"{ex.Message}\"}}}}");
            await writer.FlushAsync();
        }
        finally
        {
            await writer.DisposeAsync();
        }

        return new EmptyResult();
    }

    /// <summary>
    /// List available models (OpenAI compatible)
    /// </summary>
    [HttpGet("models")]
    public async Task<ActionResult<ModelResponse>> ListModels()
    {
        using var activity = ActivitySources.AI.StartAIActivity("ListModels");
        
        try
        {
            _logger.LogInformation("Retrieving available models");
            var models = await _chatService.GetModelsAsync();
            
            _logger.LogInformation("Retrieved {ModelCount} available models", models.Data.Count);
            activity?.SetTag("ai.models.count", models.Data.Count);
            
            return Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve models");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            return StatusCode(500, new { error = new { message = ex.Message } });
        }
    }
}
