using StellarAnvil.Api.Models.OpenAI;

namespace StellarAnvil.Api.Models.Task;

public class AgentTask
{
    public required string TaskId { get; set; }
    public TaskState State { get; set; } = TaskState.Created;
    public string? CurrentAgent { get; set; }
    public TaskPhase CurrentPhase { get; set; } = TaskPhase.BA;
    public int DeliberationCount { get; set; }
    
    /// <summary>
    /// Messages visible to the user (what they see in Cursor/VS Code)
    /// </summary>
    public List<ChatMessage> UserMessages { get; set; } = [];
    
    /// <summary>
    /// Internal agent deliberation messages (BA ↔ Sr.BA, Dev ↔ Sr.Dev, etc.)
    /// Hidden from the user until consensus is reached
    /// </summary>
    public List<ChatMessage> InternalMessages { get; set; } = [];
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Resets deliberation count when moving to a new phase
    /// </summary>
    public void ResetDeliberationForNewPhase()
    {
        DeliberationCount = 0;
        InternalMessages.Clear();
    }
}

