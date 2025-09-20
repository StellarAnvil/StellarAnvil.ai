using StellarAnvil.Domain.Enums;

namespace StellarAnvil.Domain.Entities;

public class TeamMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public TeamMemberType Type { get; set; }
    public TeamMemberRole Role { get; set; }
    public TeamMemberGrade Grade { get; set; }
    public string? Model { get; set; }
    public string SystemPromptFile { get; set; } = string.Empty;
    public Guid? CurrentTaskId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Task? CurrentTask { get; set; }
    public ICollection<Task> AssignedTasks { get; set; } = new List<Task>();
    public ICollection<TaskHistory> TaskHistories { get; set; } = new List<TaskHistory>();
}
