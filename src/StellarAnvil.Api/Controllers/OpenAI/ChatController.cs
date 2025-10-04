using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StellarAnvil.Api.Observability;
using StellarAnvil.Application.DTOs.OpenAI;
using StellarAnvil.Application.Services;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    // [HttpPost("chat/completions")]
    // [AllowAnonymous]
    // public async Task CreateChatCompletion([FromBody] ChatCompletionRequest request)
    // {
    //         Response.ContentType = "text/event-stream";
    //         Response.Headers.Add("Cache-Control", "no-cache");
    //         Response.Headers.Add("Connection", "keep-alive");

    //         var chatId = $"chatcmpl-{Guid.NewGuid().ToString("N").Substring(0, 16)}";
    //         var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    //         var modelName = request.Model ?? "gpt-3.5-turbo";

    //         // CHUNK 1: Role + initial content
    //         var chunk1 = new
    //         {
    //             id = chatId,
    //             @object = "chat.completion.chunk",
    //             created = timestamp,
    //             model = modelName,
    //             choices = new[]
    //             {
    //                 new
    //                 {
    //                     index = 0,
    //                     delta = new 
    //                     { 
    //                         role = "assistant", 
    //                         content = "" // Empty content with role
    //                     },
    //                     finish_reason = (string)null
    //                 }
    //             }
    //         };

    //         var chunk1Json = JsonSerializer.Serialize(chunk1, new JsonSerializerOptions
    //         {
    //             DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    //         });
    //         await Response.WriteAsync($"data: {chunk1Json}\n\n");
    //         await Response.Body.FlushAsync();

    //         // Add a small delay to simulate processing
    //         await Task.Delay(100);

    //         // CHUNK 2: Actual content
    //         var contentChunks = new[] 
    //         { 
    //             "Hello", 
    //             " from", 
    //             " my", 
    //             " custom", 
    //             " OpenAI", 
    //             "-compatible", 
    //             " model!", 
    //             " This", 
    //             " is", 
    //             " working", 
    //             " correctly", 
    //             " in", 
    //             " VSCode!" 
    //         };

    //         foreach (var content in contentChunks)
    //         {
    //             var contentChunk = new
    //             {
    //                 id = chatId,
    //                 @object = "chat.completion.chunk",
    //                 created = timestamp,
    //                 model = modelName,
    //                 choices = new[]
    //                 {
    //                     new
    //                     {
    //                         index = 0,
    //                         delta = new { content = content },
    //                         finish_reason = (string)null
    //                     }
    //                 }
    //             };

    //             var chunkJson = JsonSerializer.Serialize(contentChunk, new JsonSerializerOptions
    //             {
    //                 DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    //             });
    //             await Response.WriteAsync($"data: {chunkJson}\n\n");
    //             await Response.Body.FlushAsync();
    //             await Task.Delay(50); // Small delay between chunks for effect
    //         }

    //         // FINAL CHUNK: Empty content with finish_reason
    //         var finalChunk = new
    //         {
    //             id = chatId,
    //             @object = "chat.completion.chunk",
    //             created = timestamp,
    //             model = modelName,
    //             choices = new[]
    //             {
    //                 new
    //                 {
    //                     index = 0,
    //                     delta = new { }, // Empty delta
    //                     finish_reason = "stop"
    //                 }
    //             }
    //         };

    //         var finalChunkJson = JsonSerializer.Serialize(finalChunk);
    //         await Response.WriteAsync($"data: {finalChunkJson}\n\n");
    //         await Response.Body.FlushAsync();

    //         // Send [DONE] marker
    //         await Response.WriteAsync("data: [DONE]\n\n");
    //         await Response.Body.FlushAsync();

    // }

    [HttpPost("chat/completions")]
    [AllowAnonymous]
    public async Task CreateChatCompletion([FromBody] ChatCompletionRequest request)
    {
        var fullRequestJson = JsonSerializer.Serialize(request);
        _logger.LogInformation("Received chat completion request: {RequestJson}", fullRequestJson);
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        var chatId = $"chatcmpl-{Guid.NewGuid().ToString("N").Substring(0, 16)}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var modelName = request.Model ?? "gpt-3.5-turbo";

        var chunk1 = new ChatCompletionResponse
        {
            Id = chatId,
            Object = "chat.completion.chunk",
            Created = timestamp,
            Model = modelName,
            Choices =
            [
                new Choice
                {
                    Index = 0,
                    Delta = new ChatMessage
                    {
                        Role = "assistant",
                        Content = "" // Empty content with role
                    },
                    FinishReason = null
                }
            ]
        };


        var chunk1Json = JsonSerializer.Serialize(chunk1, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        await Response.WriteAsync($"data: {chunk1Json}\n\n");
        await Response.Body.FlushAsync();

        // Add a small delay to simulate processing
        await Task.Delay(100);

        // CHUNK 2: Actual content
        await foreach (var chunk in _chatService.ProcessChatWithFunctionCallsAsync(request))
        {
            var json = JsonSerializer.Serialize(chunk, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            await Response.WriteAsync($"data: {json}\n\n");
            await Response.Body.FlushAsync();
        }
        
        // Send the final [DONE] message
        await Response.WriteAsync("data: [DONE]\n\n");
        await Response.Body.FlushAsync();


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
                    Delta = new ChatMessage
                    {
                        Role = "assistant",
                        Content = combinedContent
                    },
                    FinishReason = lastChunk?.Choices?.FirstOrDefault()?.FinishReason ?? "stop"
                }
            },
            Usage = new Usage { CompletionTokens = 123, PromptTokens = 456, TotalTokens = 579 } // Placeholder values
        };
        
        stopwatch.Stop();
        Metrics.ChatCompletions.Add(1, new KeyValuePair<string, object?>("model", request.Model));
        Metrics.ChatCompletionDuration.Record(stopwatch.Elapsed.TotalSeconds, 
            new KeyValuePair<string, object?>("model", request.Model));

        activity?.SetTag("ai.response.finish_reason", response.Choices.FirstOrDefault()?.FinishReason);
        activity?.SetTag("ai.response.usage.total_tokens", response.Usage?.TotalTokens);

        _logger.LogInformation("Chat completion processed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

        var finalResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        _logger.LogInformation("Chat completion processed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

        return new ContentResult
        {
            Content = finalResponse,
            ContentType = "application/json; charset=utf-8",
            StatusCode = 200
        };
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
    [AllowAnonymous]
    public async Task<ActionResult<ModelResponse>> ListModels()
    {
        using var activity = ActivitySources.AI.StartAIActivity("ListModels");
        
        try
        {
            _logger.LogInformation("Retrieving available models");
            var models = await _chatService.GetModelsAsync();

            models.Data.Add(new Model
            {
                Id = "my-gpt-4",
                Object = "model",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                OwnedBy = "custom-provider",
            });
            
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
