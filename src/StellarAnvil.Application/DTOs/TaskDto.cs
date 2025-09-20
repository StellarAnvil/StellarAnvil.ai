using StellarAnvil.Domain.Enums;

namespace StellarAnvil.Application.DTOs;

public class TaskDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public WorkflowState CurrentState { get; set; }
    public Guid? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public Guid WorkflowId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateTaskDto
{
    public string Description { get; set; } = string.Empty;
}

public class UpdateTaskDto
{
    public string? Description { get; set; }
    public WorkflowState? CurrentState { get; set; }
    public Guid? AssigneeId { get; set; }
}
