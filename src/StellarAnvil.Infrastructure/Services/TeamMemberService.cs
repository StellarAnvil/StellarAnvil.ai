using Microsoft.EntityFrameworkCore;
using StellarAnvil.Domain.Entities;
using StellarAnvil.Domain.Enums;
using StellarAnvil.Domain.Services;
using StellarAnvil.Infrastructure.Data;
using StellarAnvil.Infrastructure.Repositories;

namespace StellarAnvil.Infrastructure.Services;

public class TeamMemberService : ITeamMemberService
{
    private readonly StellarAnvilDbContext _context;
    private readonly IRepository<TeamMember> _teamMemberRepository;

    public TeamMemberService(
        StellarAnvilDbContext context,
        IRepository<TeamMember> teamMemberRepository)
    {
        _context = context;
        _teamMemberRepository = teamMemberRepository;
    }

    public async Task<TeamMember?> GetAvailableTeamMemberAsync(TeamMemberRole role, TeamMemberGrade preferredGrade = TeamMemberGrade.Junior)
    {
        // First try to find AI members with preferred grade
        var availableAiMembers = await _context.TeamMembers
            .Where(tm => tm.Role == role && 
                        tm.Type == TeamMemberType.AI && 
                        tm.CurrentTaskId == null &&
                        tm.Grade == preferredGrade)
            .FirstOrDefaultAsync();

        if (availableAiMembers != null)
            return availableAiMembers;

        // If no AI with preferred grade, try other AI grades (Junior -> Senior -> Lead)
        var gradeOrder = preferredGrade == TeamMemberGrade.Junior 
            ? new[] { TeamMemberGrade.Senior, TeamMemberGrade.Lead }
            : preferredGrade == TeamMemberGrade.Senior
                ? new[] { TeamMemberGrade.Junior, TeamMemberGrade.Lead }
                : new[] { TeamMemberGrade.Junior, TeamMemberGrade.Senior };

        foreach (var grade in gradeOrder)
        {
            var aiMember = await _context.TeamMembers
                .Where(tm => tm.Role == role && 
                            tm.Type == TeamMemberType.AI && 
                            tm.CurrentTaskId == null &&
                            tm.Grade == grade)
                .FirstOrDefaultAsync();

            if (aiMember != null)
                return aiMember;
        }

        // If no AI available, try human members
        return await _context.TeamMembers
            .Where(tm => tm.Role == role && 
                        tm.Type == TeamMemberType.Human && 
                        tm.CurrentTaskId == null)
            .OrderBy(tm => tm.Grade)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> AssignTaskAsync(Guid teamMemberId, Guid taskId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // Check if team member is available
            var teamMember = await _context.TeamMembers
                .FirstOrDefaultAsync(tm => tm.Id == teamMemberId && tm.CurrentTaskId == null);

            if (teamMember == null)
                return false;

            // Assign the task
            teamMember.CurrentTaskId = taskId;
            teamMember.UpdatedAt = DateTime.UtcNow;

            // Update the task assignee
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task != null)
            {
                task.AssigneeId = teamMemberId;
                task.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    public async Task<bool> UnassignTaskAsync(Guid teamMemberId)
    {
        var teamMember = await _teamMemberRepository.GetByIdAsync(teamMemberId);
        if (teamMember == null || teamMember.CurrentTaskId == null)
            return false;

        // Update task to remove assignee
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == teamMember.CurrentTaskId);
        if (task != null)
        {
            task.AssigneeId = null;
            task.UpdatedAt = DateTime.UtcNow;
        }

        // Clear current task from team member
        teamMember.CurrentTaskId = null;
        teamMember.UpdatedAt = DateTime.UtcNow;

        await _teamMemberRepository.UpdateAsync(teamMember);
        return true;
    }

    public async Task<TeamMember?> GetTeamMemberByNameAsync(string name)
    {
        return await _context.TeamMembers
            .FirstOrDefaultAsync(tm => tm.Name.ToLower() == name.ToLower());
    }

    public async Task<TeamMember?> GetByIdAsync(Guid id)
    {
        return await _context.TeamMembers.FindAsync(id);
    }

    public async Task<List<TeamMember>> GetAvailableByRoleAsync(TeamMemberRole role)
    {
        return await _context.TeamMembers
            .Where(tm => tm.Role == role && tm.CurrentTaskId == null)
            .OrderBy(tm => tm.Grade) // Junior first, then Senior
            .ToListAsync();
    }

    public async Task<bool> IsTeamMemberAvailableAsync(Guid teamMemberId)
    {
        var teamMember = await _teamMemberRepository.GetByIdAsync(teamMemberId);
        return teamMember?.CurrentTaskId == null;
    }
}
