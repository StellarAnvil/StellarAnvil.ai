using AutoMapper;
using StellarAnvil.Application.DTOs;
using StellarAnvil.Domain.Entities;
using StellarAnvil.Domain.Enums;
using StellarAnvil.Domain.Services;
using StellarAnvil.Infrastructure.Repositories;

namespace StellarAnvil.Application.Services;

public class TeamMemberApplicationService : ITeamMemberApplicationService
{
    private readonly IRepository<TeamMember> _repository;
    private readonly IMapper _mapper;
    private readonly ISystemPromptService _systemPromptService;

    public TeamMemberApplicationService(IRepository<TeamMember> repository, IMapper mapper, ISystemPromptService systemPromptService)
    {
        _repository = repository;
        _mapper = mapper;
        _systemPromptService = systemPromptService;
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
        
        // Set default system prompt if not provided
        if (string.IsNullOrEmpty(teamMember.SystemPrompt))
        {
            teamMember.SystemPrompt = _systemPromptService.GetDefaultSystemPrompt(teamMember.Role);
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

}
