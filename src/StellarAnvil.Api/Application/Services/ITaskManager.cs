using StellarAnvil.Api.Application.DTOs;
using StellarAnvil.Api.Application.Results;
using StellarAnvil.Api.Domain.Entities;

namespace StellarAnvil.Api.Application.Services;

/// <summary>
/// Manages task lifecycle: creation, loading, state transitions, and conversation history.
/// </summary>
public interface ITaskManager
{
    /// <summary>
    /// Resolves the task for a chat completion request.
    /// Creates a new task for fresh chats, loads existing task for continuations.
    /// </summary>
    Task<AgentTask> ResolveTaskAsync(ChatCompletionRequest request);
    
    /// <summary>
    /// Updates task state after tool calls are detected.
    /// Stores pending tool calls and transitions to AwaitingToolResult state.
    /// </summary>
    Task UpdateForToolCallsAsync(AgentTask task, List<RequestedToolCall> toolCalls, string? agentId);
    
    /// <summary>
    /// Updates task state after a text response.
    /// Stores the response and transitions to appropriate state.
    /// </summary>
    Task UpdateForTextResponseAsync(AgentTask task, string response, bool isComplete);
}

