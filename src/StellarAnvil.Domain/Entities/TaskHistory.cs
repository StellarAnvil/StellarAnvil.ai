using StellarAnvil.Domain.Enums;

namespace StellarAnvil.Domain.Entities;

public class TaskHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public Guid? TeamMemberId { get; set; }
    public WorkflowState FromState { get; set; }
    public WorkflowState ToState { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Task Task { get; set; } = null!;
    public TeamMember? TeamMember { get; set; }
}
