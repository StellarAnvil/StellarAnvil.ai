using AutoMapper;
using StellarAnvil.Application.DTOs;
using StellarAnvil.Domain.Entities;
using StellarAnvil.Domain.Enums;
using StellarAnvil.Infrastructure.Repositories;

namespace StellarAnvil.Application.Services;

public class TeamMemberApplicationService : ITeamMemberApplicationService
{
    private readonly IRepository<TeamMember> _repository;
    private readonly IMapper _mapper;

    public TeamMemberApplicationService(IRepository<TeamMember> repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<TeamMemberDto>> GetAllAsync()
    {
        var teamMembers = await _repository.GetAllAsync();
        return _mapper.Map<IEnumerable<TeamMemberDto>>(teamMembers);
    }

    public async Task<TeamMemberDto?> GetByIdAsync(Guid id)
    {
        var teamMember = await _repository.GetByIdAsync(id);
        return teamMember != null ? _mapper.Map<TeamMemberDto>(teamMember) : null;
    }

    public async Task<TeamMemberDto> CreateAsync(CreateTeamMemberDto createDto)
    {
        var teamMember = _mapper.Map<TeamMember>(createDto);
        
        // Set default system prompt file if not provided
        if (string.IsNullOrEmpty(teamMember.SystemPromptFile))
        {
            teamMember.SystemPromptFile = GetDefaultSystemPromptFile(teamMember.Role);
        }

        var createdTeamMember = await _repository.AddAsync(teamMember);
        return _mapper.Map<TeamMemberDto>(createdTeamMember);
    }

    public async Task<TeamMemberDto?> UpdateAsync(Guid id, UpdateTeamMemberDto updateDto)
    {
        var existingTeamMember = await _repository.GetByIdAsync(id);
        if (existingTeamMember == null)
            return null;

        _mapper.Map(updateDto, existingTeamMember);
        existingTeamMember.UpdatedAt = DateTime.UtcNow;

        var updatedTeamMember = await _repository.UpdateAsync(existingTeamMember);
        return _mapper.Map<TeamMemberDto>(updatedTeamMember);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var teamMember = await _repository.GetByIdAsync(id);
        if (teamMember == null)
            return false;

        await _repository.DeleteAsync(teamMember);
        return true;
    }

    public async Task<TeamMemberDto?> GetByNameAsync(string name)
    {
        var teamMember = await _repository.FirstOrDefaultAsync(tm => tm.Name.ToLower() == name.ToLower());
        return teamMember != null ? _mapper.Map<TeamMemberDto>(teamMember) : null;
    }

    private static string GetDefaultSystemPromptFile(TeamMemberRole role)
    {
        return role switch
        {
            TeamMemberRole.ProductOwner => "prompts/product-owner.txt",
            TeamMemberRole.BusinessAnalyst => "prompts/business-analyst.txt",
            TeamMemberRole.Architect => "prompts/architect.txt",
            TeamMemberRole.UXDesigner => "prompts/ux-designer.txt",
            TeamMemberRole.Developer => "prompts/developer.txt",
            TeamMemberRole.QualityAssurance => "prompts/quality-assurance.txt",
            TeamMemberRole.SecurityReviewer => "prompts/security-reviewer.txt",
            _ => "prompts/default.txt"
        };
    }
}
