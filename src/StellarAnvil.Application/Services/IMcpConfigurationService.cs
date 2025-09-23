using StellarAnvil.Application.DTOs;

namespace StellarAnvil.Application.Services;

public interface IMcpConfigurationService
{
    Task<IEnumerable<McpConfigurationDto>> GetAllAsync();
    Task<McpConfigurationDto?> GetByIdAsync(Guid id);
    Task<McpConfigurationDto> CreateAsync(CreateMcpConfigurationDto createDto);
    Task<McpConfigurationDto?> UpdateAsync(Guid id, UpdateMcpConfigurationDto updateDto);
    Task<bool> DeleteAsync(Guid id);
    Task<McpConnectionTestResult> TestConnectionAsync(Guid id);
    Task<McpConfigurationDto?> GetByTypeAsync(string type);
}