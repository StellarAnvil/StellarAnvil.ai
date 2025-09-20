using StellarAnvil.Domain.Enums;

namespace StellarAnvil.Application.DTOs;

public class TeamMemberDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public TeamMemberType Type { get; set; }
    public TeamMemberRole Role { get; set; }
    public TeamMemberGrade Grade { get; set; }
    public string? Model { get; set; }
    public string SystemPromptFile { get; set; } = string.Empty;
    public Guid? CurrentTaskId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateTeamMemberDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public TeamMemberType Type { get; set; }
    public TeamMemberRole Role { get; set; }
    public TeamMemberGrade Grade { get; set; }
    public string? Model { get; set; }
    public string? SystemPromptFile { get; set; }
}

public class UpdateTeamMemberDto
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public TeamMemberType? Type { get; set; }
    public TeamMemberRole? Role { get; set; }
    public TeamMemberGrade? Grade { get; set; }
    public string? Model { get; set; }
    public string? SystemPromptFile { get; set; }
}
