using StellarAnvil.Application.DTOs;

namespace StellarAnvil.Application.Services;

public interface ITeamMemberApplicationService
{
    Task<IEnumerable<TeamMemberDto>> GetAllAsync();
    Task<TeamMemberDto?> GetByIdAsync(Guid id);
    Task<TeamMemberDto> CreateAsync(CreateTeamMemberDto createDto);
    Task<TeamMemberDto?> UpdateAsync(Guid id, UpdateTeamMemberDto updateDto);
    Task<bool> DeleteAsync(Guid id);
    Task<TeamMemberDto?> GetByNameAsync(string name);
}
