using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StellarAnvil.Application.DTOs;
using StellarAnvil.Application.Services;

namespace StellarAnvil.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class TasksController : ControllerBase
{
    private readonly ITaskApplicationService _taskService;

    public TasksController(ITaskApplicationService taskService)
    {
        _taskService = taskService;
    }

    /// <summary>
    /// Get all tasks
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskDto>>> GetAll()
    {
        var tasks = await _taskService.GetAllAsync();
        return Ok(tasks);
    }

    /// <summary>
    /// Get task by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TaskDto>> GetById(Guid id)
    {
        var task = await _taskService.GetByIdAsync(id);
        if (task == null)
        {
            return NotFound(new { message = "Task not found" });
        }

        return Ok(task);
    }

    /// <summary>
    /// Create a new task
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create(CreateTaskDto createDto)
    {
        try
        {
            var task = await _taskService.CreateAsync(createDto);
            return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing task
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<TaskDto>> Update(Guid id, UpdateTaskDto updateDto)
    {
        try
        {
            var task = await _taskService.UpdateAsync(id, updateDto);
            if (task == null)
            {
                return NotFound(new { message = "Task not found" });
            }

            return Ok(task);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a task
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _taskService.DeleteAsync(id);
        if (!success)
        {
            return NotFound(new { message = "Task not found" });
        }

        return NoContent();
    }

    /// <summary>
    /// Get workflow progress for a task
    /// </summary>
    [HttpGet("{id}/workflow")]
    public async Task<ActionResult<WorkflowProgress>> GetWorkflowProgress(Guid id)
    {
        try
        {
            var progress = await _taskService.GetWorkflowProgressAsync(id);
            return Ok(progress);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get available workflow triggers for a task
    /// </summary>
    [HttpGet("{id}/triggers")]
    public async Task<ActionResult<List<WorkflowTrigger>>> GetAvailableTriggers(Guid id)
    {
        try
        {
            var triggers = await _taskService.GetAvailableTriggersAsync(id);
            return Ok(triggers);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Execute a workflow transition
    /// </summary>
    [HttpPost("{id}/transition")]
    public async Task<IActionResult> TransitionWorkflow(
        Guid id, 
        [FromBody] TransitionWorkflowRequest request)
    {
        try
        {
            var success = await _taskService.TransitionWorkflowAsync(id, request.Trigger, request.ExecutedBy);
            if (!success)
            {
                return BadRequest(new { message = "Transition not allowed or failed" });
            }

            return Ok(new { message = "Workflow transition executed successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}

public class TransitionWorkflowRequest
{
    public WorkflowTrigger Trigger { get; set; }
    public Guid? ExecutedBy { get; set; }
}
