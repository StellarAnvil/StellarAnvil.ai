using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StellarAnvil.Api.Observability;
using StellarAnvil.Application.DTOs.OpenAI;
using StellarAnvil.Application.Services;
using System.Diagnostics;

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
    public async Task<ActionResult<ChatCompletionResponse>> CreateChatCompletion([FromBody] ChatCompletionRequest request)
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

            _logger.LogInformation("Processing chat completion request with {MessageCount} messages using model {Model}", 
                request.Messages.Count, request.Model);

            activity?.SetTag("ai.request.message_count", request.Messages.Count);
            activity?.SetTag("ai.request.temperature", request.Temperature);
            activity?.SetTag("ai.request.max_tokens", request.MaxTokens);

            var response = await _chatService.ProcessChatCompletionAsync(request);
            
            stopwatch.Stop();
            Metrics.ChatCompletions.Add(1, new KeyValuePair<string, object?>("model", request.Model));
            Metrics.ChatCompletionDuration.Record(stopwatch.Elapsed.TotalSeconds, 
                new KeyValuePair<string, object?>("model", request.Model));

            activity?.SetTag("ai.response.finish_reason", response.Choices.FirstOrDefault()?.FinishReason);
            activity?.SetTag("ai.response.usage.total_tokens", response.Usage?.TotalTokens);

            _logger.LogInformation("Chat completion processed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to process chat completion request");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            return StatusCode(500, new { error = new { message = ex.Message } });
        }
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
