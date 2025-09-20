using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StellarAnvil.Api.Observability;
using StellarAnvil.Application.DTOs;
using StellarAnvil.Application.Services;
using System.Diagnostics;

namespace StellarAnvil.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class TeamMembersController : ControllerBase
{
    private readonly ITeamMemberApplicationService _teamMemberService;
    private readonly ILogger<TeamMembersController> _logger;

    public TeamMembersController(ITeamMemberApplicationService teamMemberService, ILogger<TeamMembersController> logger)
    {
        _teamMemberService = teamMemberService;
        _logger = logger;
    }

    /// <summary>
    /// Get all team members
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TeamMemberDto>>> GetAll()
    {
        using var activity = ActivitySources.StellarAnvil.StartActivity("GetAllTeamMembers");
        
        _logger.LogInformation("Retrieving all team members");
        var teamMembers = await _teamMemberService.GetAllAsync();
        
        _logger.LogInformation("Retrieved {Count} team members", teamMembers.Count());
        return Ok(teamMembers);
    }

    /// <summary>
    /// Get team member by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TeamMemberDto>> GetById(Guid id)
    {
        using var activity = ActivitySources.StellarAnvil.StartActivity("GetTeamMemberById");
        activity?.SetTag("team_member.id", id.ToString());
        
        _logger.LogInformation("Retrieving team member {Id}", id);
        var teamMember = await _teamMemberService.GetByIdAsync(id);
        
        if (teamMember == null)
        {
            _logger.LogWarning("Team member {Id} not found", id);
            return NotFound(new { message = "Team member not found" });
        }

        _logger.LogInformation("Retrieved team member {Id}: {Name}", id, teamMember.Name);
        return Ok(teamMember);
    }

    /// <summary>
    /// Create a new team member
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TeamMemberDto>> Create(CreateTeamMemberDto createDto)
    {
        using var activity = ActivitySources.StellarAnvil.StartActivity("CreateTeamMember");
        activity?.SetTag("team_member.name", createDto.Name);
        activity?.SetTag("team_member.role", createDto.Role.ToString());
        
        try
        {
            _logger.LogInformation("Creating new team member: {Name} ({Role})", createDto.Name, createDto.Role);
            var teamMember = await _teamMemberService.CreateAsync(createDto);
            
            Metrics.AvailableTeamMembers.Add(1);
            _logger.LogInformation("Created team member {Id}: {Name}", teamMember.Id, teamMember.Name);
            
            return CreatedAtAction(nameof(GetById), new { id = teamMember.Id }, teamMember);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create team member: {Name}", createDto.Name);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing team member
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<TeamMemberDto>> Update(Guid id, UpdateTeamMemberDto updateDto)
    {
        using var activity = ActivitySources.StellarAnvil.StartActivity("UpdateTeamMember");
        activity?.SetTag("team_member.id", id.ToString());
        
        try
        {
            _logger.LogInformation("Updating team member {Id}", id);
            var teamMember = await _teamMemberService.UpdateAsync(id, updateDto);
            
            if (teamMember == null)
            {
                _logger.LogWarning("Team member {Id} not found for update", id);
                return NotFound(new { message = "Team member not found" });
            }

            _logger.LogInformation("Updated team member {Id}: {Name}", id, teamMember.Name);
            return Ok(teamMember);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update team member {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a team member
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        using var activity = ActivitySources.StellarAnvil.StartActivity("DeleteTeamMember");
        activity?.SetTag("team_member.id", id.ToString());
        
        _logger.LogInformation("Deleting team member {Id}", id);
        var success = await _teamMemberService.DeleteAsync(id);
        
        if (!success)
        {
            _logger.LogWarning("Team member {Id} not found for deletion", id);
            return NotFound(new { message = "Team member not found" });
        }

        Metrics.AvailableTeamMembers.Add(-1);
        _logger.LogInformation("Deleted team member {Id}", id);
        return NoContent();
    }
}
