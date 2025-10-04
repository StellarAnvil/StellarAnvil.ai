using AutoGen.Core;
using Google.Cloud.AIPlatform.V1;

namespace StellarAnvil.Domain.Services;

/// <summary>
/// Service interface for AutoGen Gemini chat agent operations
/// </summary>
public interface IAutoGenGeminiService
{
    /// <summary>
    /// Creates a Gemini chat agent for the specified model
    /// </summary>
    /// <param name="model">The Gemini model to use (e.g., "gemini-2.5-pro")</param>
    /// <param name="systemMessage">Optional system message for the agent</param>
    /// <param name="tools">Optional tools/functions for the agent</param>
    /// <returns>An AutoGen chat agent</returns>
    Task<IAgent> CreateGeminiAgentAsync(string model, string? systemMessage = null, Google.Cloud.AIPlatform.V1.Tool[]? tools = null);
    
    /// <summary>
    /// Sends a message to the agent and gets a streaming response
    /// </summary>
    /// <param name="agent">The AutoGen agent</param>
    /// <param name="message">The message to send</param>
    /// <returns>Streaming response from the agent</returns>
    IAsyncEnumerable<IMessage> SendMessageStreamAsync(IAgent agent, string message);
    
    /// <summary>
    /// Sends multiple messages in a conversation to the agent
    /// </summary>
    /// <param name="agent">The AutoGen agent</param>
    /// <param name="messages">The conversation messages</param>
    /// <returns>The agent's response</returns>
    Task<IMessage> SendConversationAsync(IAgent agent, IEnumerable<IMessage<Content>> messages);
}