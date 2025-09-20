using StellarAnvil.Domain.Enums;

namespace StellarAnvil.Domain.Entities;

public class Workflow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<WorkflowTransition> Transitions { get; set; } = new List<WorkflowTransition>();
    public ICollection<Task> Tasks { get; set; } = new List<Task>();
}

public class WorkflowTransition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowId { get; set; }
    public WorkflowState FromState { get; set; }
    public WorkflowState ToState { get; set; }
    public TeamMemberRole RequiredRole { get; set; }
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Workflow Workflow { get; set; } = null!;
}
