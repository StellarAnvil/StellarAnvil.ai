using AutoMapper;
using Microsoft.Extensions.Logging;
using StellarAnvil.Application.DTOs;
using StellarAnvil.Domain.Entities;
using StellarAnvil.Infrastructure.Repositories;
using System.Text.Json;

namespace StellarAnvil.Application.Services;

public class McpConfigurationService : IMcpConfigurationService
{
    private readonly IRepository<McpConfiguration> _mcpRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<McpConfigurationService> _logger;
    private readonly HttpClient _httpClient;

    public McpConfigurationService(
        IRepository<McpConfiguration> mcpRepository,
        IMapper mapper,
        ILogger<McpConfigurationService> logger,
        HttpClient httpClient)
    {
        _mcpRepository = mcpRepository;
        _mapper = mapper;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<McpConfigurationDto>> GetAllAsync()
    {
        var mcpConfigs = await _mcpRepository.GetAllAsync();
        return _mapper.Map<IEnumerable<McpConfigurationDto>>(mcpConfigs);
    }

    public async Task<McpConfigurationDto?> GetByIdAsync(Guid id)
    {
        var mcpConfig = await _mcpRepository.GetByIdAsync(id);
        return mcpConfig == null ? null : _mapper.Map<McpConfigurationDto>(mcpConfig);
    }

    public async Task<McpConfigurationDto?> GetByTypeAsync(string type)
    {
        var mcpConfigs = await _mcpRepository.FindAsync(m => m.Type == type && m.IsActive);
        var mcpConfig = mcpConfigs.FirstOrDefault();
        return mcpConfig == null ? null : _mapper.Map<McpConfigurationDto>(mcpConfig);
    }

    public async Task<McpConfigurationDto> CreateAsync(CreateMcpConfigurationDto createDto)
    {
        var mcpConfig = new McpConfiguration
        {
            Name = createDto.Name,
            Type = createDto.Type,
            ApiKey = createDto.ApiKey,
            BaseUrl = createDto.BaseUrl,
            Settings = createDto.Settings?.Count > 0 ? JsonSerializer.Serialize(createDto.Settings) : "{}",
            IsActive = true
        };

        await _mcpRepository.AddAsync(mcpConfig);
        return _mapper.Map<McpConfigurationDto>(mcpConfig);
    }

    public async Task<McpConfigurationDto?> UpdateAsync(Guid id, UpdateMcpConfigurationDto updateDto)
    {
        var mcpConfig = await _mcpRepository.GetByIdAsync(id);
        if (mcpConfig == null)
            return null;

        if (!string.IsNullOrEmpty(updateDto.Name))
            mcpConfig.Name = updateDto.Name;

        if (!string.IsNullOrEmpty(updateDto.ApiKey))
            mcpConfig.ApiKey = updateDto.ApiKey;

        if (!string.IsNullOrEmpty(updateDto.BaseUrl))
            mcpConfig.BaseUrl = updateDto.BaseUrl;

        if (updateDto.Settings != null)
            mcpConfig.Settings = updateDto.Settings.Count > 0 ? JsonSerializer.Serialize(updateDto.Settings) : "{}";

        if (updateDto.IsActive.HasValue)
            mcpConfig.IsActive = updateDto.IsActive.Value;

        mcpConfig.UpdatedAt = DateTime.UtcNow;

        await _mcpRepository.UpdateAsync(mcpConfig);
        return _mapper.Map<McpConfigurationDto>(mcpConfig);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var mcpConfig = await _mcpRepository.GetByIdAsync(id);
        if (mcpConfig == null)
            return false;

        await _mcpRepository.DeleteAsync(mcpConfig);
        return true;
    }

    public async Task<McpConnectionTestResult> TestConnectionAsync(Guid id)
    {
        var mcpConfig = await _mcpRepository.GetByIdAsync(id);
        if (mcpConfig == null)
        {
            return new McpConnectionTestResult
            {
                Success = false,
                ErrorMessage = "MCP configuration not found"
            };
        }

        try
        {
            // Test connection based on MCP type
            return mcpConfig.Type.ToLower() switch
            {
                "jira" => await TestJiraConnectionAsync(mcpConfig),
                "github" => await TestGitHubConnectionAsync(mcpConfig),
                "slack" => await TestSlackConnectionAsync(mcpConfig),
                _ => new McpConnectionTestResult
                {
                    Success = false,
                    ErrorMessage = $"Testing not implemented for MCP type: {mcpConfig.Type}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing MCP connection {Id}", id);
            return new McpConnectionTestResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<McpConnectionTestResult> TestJiraConnectionAsync(McpConfiguration config)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{config.BaseUrl.TrimEnd('/')}/rest/api/2/serverInfo");
            request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var serverInfo = JsonSerializer.Deserialize<JsonElement>(content);

                return new McpConnectionTestResult
                {
                    Success = true,
                    Details = new Dictionary<string, object>
                    {
                        ["status"] = "Connected",
                        ["serverTitle"] = serverInfo.TryGetProperty("serverTitle", out var title) ? title.GetString() : "Unknown",
                        ["version"] = serverInfo.TryGetProperty("version", out var version) ? version.GetString() : "Unknown"
                    }
                };
            }
            else
            {
                return new McpConnectionTestResult
                {
                    Success = false,
                    ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
                };
            }
        }
        catch (Exception ex)
        {
            return new McpConnectionTestResult
            {
                Success = false,
                ErrorMessage = $"Connection failed: {ex.Message}"
            };
        }
    }

    private async Task<McpConnectionTestResult> TestGitHubConnectionAsync(McpConfiguration config)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            request.Headers.Add("Authorization", $"token {config.ApiKey}");
            request.Headers.Add("User-Agent", "StellarAnvil");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var userInfo = JsonSerializer.Deserialize<JsonElement>(content);

                return new McpConnectionTestResult
                {
                    Success = true,
                    Details = new Dictionary<string, object>
                    {
                        ["status"] = "Connected",
                        ["login"] = userInfo.TryGetProperty("login", out var login) ? login.GetString() : "Unknown",
                        ["name"] = userInfo.TryGetProperty("name", out var name) ? name.GetString() : "Unknown"
                    }
                };
            }
            else
            {
                return new McpConnectionTestResult
                {
                    Success = false,
                    ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
                };
            }
        }
        catch (Exception ex)
        {
            return new McpConnectionTestResult
            {
                Success = false,
                ErrorMessage = $"Connection failed: {ex.Message}"
            };
        }
    }

    private System.Threading.Tasks.Task<McpConnectionTestResult> TestSlackConnectionAsync(McpConfiguration config)
    {
        // Mock implementation for Slack
        return System.Threading.Tasks.Task.FromResult(new McpConnectionTestResult
        {
            Success = true,
            Details = new Dictionary<string, object>
            {
                ["status"] = "Mock connection test - not implemented"
            }
        });
    }
}