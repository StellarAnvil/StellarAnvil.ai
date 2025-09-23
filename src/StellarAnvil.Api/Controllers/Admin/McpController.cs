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
public class McpController : ControllerBase
{
    private readonly IMcpConfigurationService _mcpService;
    private readonly ILogger<McpController> _logger;

    public McpController(IMcpConfigurationService mcpService, ILogger<McpController> logger)
    {
        _mcpService = mcpService;
        _logger = logger;
    }

    /// <summary>
    /// Get all MCP configurations
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<McpConfigurationDto>>> GetAll()
    {
        using var activity = ActivitySources.StellarAnvil.StartActivity("GetAllMcpConfigurations");

        _logger.LogInformation("Retrieving all MCP configurations");
        var mcpConfigs = await _mcpService.GetAllAsync();

        _logger.LogInformation("Retrieved {Count} MCP configurations", mcpConfigs.Count());
        return Ok(mcpConfigs);
    }

    /// <summary>
    /// Get MCP configuration by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<McpConfigurationDto>> GetById(Guid id)
    {
        using var activity = ActivitySources.StellarAnvil.StartActivity("GetMcpConfigurationById");
        activity?.SetTag("mcp.configuration.id", id.ToString());

        _logger.LogInformation("Retrieving MCP configuration {Id}", id);
        var mcpConfig = await _mcpService.GetByIdAsync(id);

        if (mcpConfig == null)
        {
            _logger.LogWarning("MCP configuration {Id} not found", id);
            return NotFound(new { message = "MCP configuration not found" });
        }

        _logger.LogInformation("Retrieved MCP configuration {Id}: {Name}", id, mcpConfig.Name);
        return Ok(mcpConfig);
    }

    /// <summary>
    /// Create a new MCP configuration
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<McpConfigurationDto>> Create(CreateMcpConfigurationDto createDto)
    {
        using var activity = ActivitySources.StellarAnvil.StartActivity("CreateMcpConfiguration");
        activity?.SetTag("mcp.configuration.name", createDto.Name);
        activity?.SetTag("mcp.configuration.type", createDto.Type);

        try
        {
            _logger.LogInformation("Creating new MCP configuration: {Name} ({Type})", createDto.Name, createDto.Type);
            var mcpConfig = await _mcpService.CreateAsync(createDto);

            _logger.LogInformation("Created MCP configuration {Id}: {Name}", mcpConfig.Id, mcpConfig.Name);

            return CreatedAtAction(nameof(GetById), new { id = mcpConfig.Id }, mcpConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MCP configuration: {Name}", createDto.Name);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing MCP configuration
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<McpConfigurationDto>> Update(Guid id, UpdateMcpConfigurationDto updateDto)
    {
        using var activity = ActivitySources.StellarAnvil.StartActivity("UpdateMcpConfiguration");
        activity?.SetTag("mcp.configuration.id", id.ToString());

        try
        {
            _logger.LogInformation("Updating MCP configuration {Id}", id);
            var mcpConfig = await _mcpService.UpdateAsync(id, updateDto);

            if (mcpConfig == null)
            {
                _logger.LogWarning("MCP configuration {Id} not found for update", id);
                return NotFound(new { message = "MCP configuration not found" });
            }

            _logger.LogInformation("Updated MCP configuration {Id}: {Name}", id, mcpConfig.Name);
            return Ok(mcpConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update MCP configuration {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete an MCP configuration
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        using var activity = ActivitySources.StellarAnvil.StartActivity("DeleteMcpConfiguration");
        activity?.SetTag("mcp.configuration.id", id.ToString());

        _logger.LogInformation("Deleting MCP configuration {Id}", id);
        var success = await _mcpService.DeleteAsync(id);

        if (!success)
        {
            _logger.LogWarning("MCP configuration {Id} not found for deletion", id);
            return NotFound(new { message = "MCP configuration not found" });
        }

        _logger.LogInformation("Deleted MCP configuration {Id}", id);
        return NoContent();
    }

    /// <summary>
    /// Test MCP connection
    /// </summary>
    [HttpPost("{id}/test")]
    public async Task<ActionResult> TestConnection(Guid id)
    {
        using var activity = ActivitySources.StellarAnvil.StartActivity("TestMcpConnection");
        activity?.SetTag("mcp.configuration.id", id.ToString());

        try
        {
            _logger.LogInformation("Testing MCP connection {Id}", id);
            var result = await _mcpService.TestConnectionAsync(id);

            if (result.Success)
            {
                _logger.LogInformation("MCP connection test successful for {Id}", id);
                return Ok(new { success = true, message = "Connection successful", details = result.Details });
            }
            else
            {
                _logger.LogWarning("MCP connection test failed for {Id}: {Error}", id, result.ErrorMessage);
                return BadRequest(new { success = false, message = result.ErrorMessage, details = result.Details });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing MCP connection {Id}", id);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}