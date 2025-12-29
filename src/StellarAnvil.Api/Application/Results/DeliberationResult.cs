using StellarAnvil.Api.Application.DTOs;

namespace StellarAnvil.Api.Application.Results;

/// <summary>
/// Result of running a Manager-controlled deliberation workflow.
/// Can be either a text response or a tool call that needs client execution.
/// </summary>
public sealed record DeliberationResult
{
    /// <summary>
    /// Text response from agents (empty if this is a tool call result).
    /// </summary>
    public string Response { get; init; } = "";
    
    /// <summary>
    /// Whether the workflow is complete (all phases approved).
    /// </summary>
    public bool IsComplete { get; init; }
    
    /// <summary>
    /// Tool calls that need to be executed by the client.
    /// When non-empty, the server should emit these as OpenAI tool_calls and stop streaming.
    /// </summary>
    public List<ToolCall> ToolCalls { get; init; } = [];
    
    /// <summary>
    /// Whether this result contains tool calls that need client execution.
    /// </summary>
    public bool HasToolCalls => ToolCalls.Count > 0;
    
    /// <summary>
    /// The agent that emitted the tool calls (for resumption context).
    /// </summary>
    public string? ToolCallAgent { get; init; }
    
    // Convenience factory methods
    public static DeliberationResult TextResponse(string response, bool isComplete) =>
        new() { Response = response, IsComplete = isComplete };
    
    public static DeliberationResult ToolCallResponse(List<ToolCall> toolCalls, string? agent) =>
        new() { ToolCalls = toolCalls, ToolCallAgent = agent, IsComplete = false };
}

