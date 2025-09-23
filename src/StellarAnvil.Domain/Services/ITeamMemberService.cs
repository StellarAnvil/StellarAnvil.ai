using StellarAnvil.Domain.Entities;
using StellarAnvil.Domain.Enums;

namespace StellarAnvil.Domain.Services;

public interface ITeamMemberService
{
    Task<TeamMember?> GetAvailableTeamMemberAsync(TeamMemberRole role, TeamMemberGrade preferredGrade = TeamMemberGrade.Junior);
    Task<bool> AssignTaskAsync(Guid teamMemberId, Guid taskId);
    Task<bool> UnassignTaskAsync(Guid teamMemberId);
    Task<TeamMember?> GetTeamMemberByNameAsync(string name);
    Task<TeamMember?> GetByIdAsync(Guid id);
    Task<List<TeamMember>> GetAvailableByRoleAsync(TeamMemberRole role);
    Task<bool> IsTeamMemberAvailableAsync(Guid teamMemberId);
}
