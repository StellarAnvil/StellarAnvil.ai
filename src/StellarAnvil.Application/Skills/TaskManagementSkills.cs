using Microsoft.SemanticKernel;
using StellarAnvil.Application.DTOs;
using StellarAnvil.Application.Services;
using StellarAnvil.Domain.Services;
using StellarAnvil.Domain.Enums;
using System.ComponentModel;
using System.Text.Json;

namespace StellarAnvil.Application.Skills;

/// <summary>
/// Task management skills for creating and managing SDLC tasks
/// </summary>
public class TaskManagementSkills
{
    private readonly ITaskApplicationService _taskService;
    private readonly ITeamMemberService _teamMemberService;
    private readonly IWorkflowService _workflowService;

    public TaskManagementSkills(
        ITaskApplicationService taskService,
        ITeamMemberService teamMemberService,
        IWorkflowService workflowService)
    {
        _taskService = taskService;
        _teamMemberService = teamMemberService;
        _workflowService = workflowService;
    }

    [KernelFunction, Description("Create a new SDLC task and assign it to an appropriate team member")]
    public async Task<string> CreateTaskAsync(
        [Description("Task description - what needs to be done")] string description,
        [Description("User name who requested the task")] string userName,
        [Description("Optional workflow name (defaults to Simple SDLC Workflow)")] string? workflowName = null)
    {
        try
        {
            // Find the user in the system
            var user = await _teamMemberService.GetTeamMemberByNameAsync(userName);
            if (user == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Hey {userName}, I did not find you in my system, can you please request admin to add you?",
                    taskId = (string?)null
                });
            }

            // Select appropriate workflow
            var workflow = await _workflowService.SelectWorkflowForTaskAsync(description);
            
            // Create the task
            var task = await _taskService.CreateAsync(new Application.DTOs.CreateTaskDto
            {
                Description = description
            });

            // Find available team member (Junior -> Senior -> none)
            var availableJunior = await _teamMemberService.GetAvailableTeamMemberAsync(TeamMemberRole.BusinessAnalyst, TeamMemberGrade.Junior);
            var availableSenior = await _teamMemberService.GetAvailableTeamMemberAsync(TeamMemberRole.BusinessAnalyst, TeamMemberGrade.Senior);
            
            var assignee = availableJunior ?? availableSenior;
            
            if (assignee == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "I apologize, but no team members are currently available to take on this task. Please try again later.",
                    taskId = task.Id.ToString()
                });
            }

            // Assign the task
            await _taskService.AssignTaskAsync(task.Id, assignee.Id);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Task #{task.Id} created and assigned to {assignee.Name} ({assignee.Role} - {assignee.Grade})",
                taskId = task.Id.ToString(),
                taskNumber = task.Id,
                assignee = new
                {
                    name = assignee.Name,
                    role = assignee.Role.ToString(),
                    grade = assignee.Grade.ToString()
                },
                workflow = workflow.Name
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = $"Error creating task: {ex.Message}",
                taskId = (string?)null
            });
        }
    }

    [KernelFunction, Description("Get task details by task ID or task number")]
    public async Task<string> GetTaskAsync(
        [Description("Task ID or task number")] string taskIdentifier)
    {
        try
        {
            TaskDto? task = null;
            
            // Try to parse as task number first
            if (int.TryParse(taskIdentifier, out int taskNumber))
            {
                task = await _taskService.GetByTaskNumberAsync(taskNumber);
            }
            
            // If not found, try as GUID
            if (task is null && Guid.TryParse(taskIdentifier, out Guid parsedTaskId))
            {
                task = await _taskService.GetByIdAsync(parsedTaskId);
            }
            
            if (task is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Task {taskIdentifier} not found"
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                task = new
                {
                    id = task.Id,
                    description = task.Description,
                    currentState = task.CurrentState.ToString(),
                    assigneeId = task.AssigneeId,
                    workflowId = task.WorkflowId
                }
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = $"Error retrieving task: {ex.Message}"
            });
        }
    }

    [KernelFunction, Description("Continue working on an existing task")]
    public async Task<string> ContinueTaskAsync(
        [Description("Task ID or task number")] string taskIdentifier,
        [Description("User message or instruction")] string userMessage)
    {
        try
        {
            TaskDto? task = null;
            
            // Try to parse as task number first
            if (int.TryParse(taskIdentifier, out int taskNumber))
            {
                task = await _taskService.GetByTaskNumberAsync(taskNumber);
            }
            
            // If not found, try as GUID
            if (task is null && Guid.TryParse(taskIdentifier, out Guid parsedTaskId))
            {
                task = await _taskService.GetByIdAsync(parsedTaskId);
            }
            
            if (task is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Task {taskIdentifier} not found"
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Continuing work on Task #{task.Id}",
                taskId = task.Id.ToString(),
                taskNumber = task.Id,
                userMessage = userMessage,
                currentState = task.CurrentState.ToString()
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = $"Error continuing task: {ex.Message}"
            });
        }
    }
}
