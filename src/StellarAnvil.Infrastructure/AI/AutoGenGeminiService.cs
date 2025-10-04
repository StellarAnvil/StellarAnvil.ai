using AutoGen.Core;
using AutoGen.Gemini;
using Google.Cloud.AIPlatform.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StellarAnvil.Domain.Services;

namespace StellarAnvil.Infrastructure.AI;

/// <summary>
/// Implementation of AutoGen Gemini service for Google Gemini models
/// </summary>
public class AutoGenGeminiService : IAutoGenGeminiService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AutoGenGeminiService> _logger;
    private readonly string? _geminiApiKey;

    public AutoGenGeminiService(IConfiguration configuration, ILogger<AutoGenGeminiService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _geminiApiKey = _configuration["AI:Gemini:ApiKey"];
    }

    public async Task<IAgent> CreateGeminiAgentAsync(string model, string? systemMessage = null, Google.Cloud.AIPlatform.V1.Tool[]? tools = null)
    {
        if (string.IsNullOrEmpty(_geminiApiKey))
        {
            throw new InvalidOperationException("Gemini API key not configured");
        }

        try
        {
            var geminiAgent = new GeminiChatAgent(
                name: "gemini-agent",
                model: model,
                apiKey: _geminiApiKey,
                systemMessage: systemMessage ?? "You are a helpful AI assistant",
                tools: tools);

            _logger.LogInformation("Created Gemini agent with model: {Model} and {ToolCount} tools", model, tools?.Length ?? 0);
            return geminiAgent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Gemini agent for model: {Model}", model);
            throw;
        }
    }

    public async IAsyncEnumerable<IMessage> SendMessageStreamAsync(IAgent agent, string message)
    {
        var geminiAgent = agent as GeminiChatAgent ?? throw new ArgumentException("Invalid agent type", nameof(agent));
        var textMessage = new TextMessage(Role.User, message);
        var response = geminiAgent.GenerateStreamingReplyAsync([textMessage]);

        await foreach (var msg in response)
        {
            yield return msg;
        }
    }

    public async Task<IMessage> SendConversationAsync(IAgent agent, IEnumerable<IMessage> messages)
    {
        try
        {
            var response = await agent.GenerateReplyAsync(messages);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send conversation to agent");
            throw;
        }
    }
}