using StellarAnvil.Api.Application.DTOs;

namespace StellarAnvil.Api.Domain.Entities;

public class AgentTask
{
    public required string TaskId { get; set; }
    public TaskState State { get; set; } = TaskState.Created;
    
    /// <summary>
    /// Messages visible to the user (what they see in Cursor/VS Code)
    /// </summary>
    public List<ChatMessage> UserMessages { get; set; } = [];
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Tools available to agents for function calling (e.g., file read, codebase search)
    /// </summary>
    public List<Tool>? Tools { get; set; }
    
    /// <summary>
    /// Pending tool calls that have been sent to the client and are awaiting results.
    /// Stored so we can validate incoming tool results match expected calls.
    /// </summary>
    public List<PendingToolCall>? PendingToolCalls { get; set; }
    
    /// <summary>
    /// The agent that was executing when tool calls were emitted (for context).
    /// </summary>
    public string? LastActiveAgent { get; set; }
}

/// <summary>
/// Represents a tool call that has been sent to the client and is awaiting a result.
/// </summary>
public record PendingToolCall(string CallId, string FunctionName, string Arguments);

public enum TaskState
{
    Created,
    Working,
    AwaitingUser,
    AwaitingToolResult,
    Completed
}

