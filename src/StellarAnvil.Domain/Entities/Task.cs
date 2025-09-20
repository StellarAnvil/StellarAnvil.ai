using StellarAnvil.Domain.Enums;

namespace StellarAnvil.Domain.Entities;

public class Task
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public WorkflowState CurrentState { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid WorkflowId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public TeamMember? Assignee { get; set; }
    public Workflow Workflow { get; set; } = null!;
    public ICollection<TaskHistory> TaskHistories { get; set; } = new List<TaskHistory>();
}
