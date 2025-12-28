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
}

public enum TaskState
{
    Created,
    Working,
    AwaitingUser,
    Completed
}

