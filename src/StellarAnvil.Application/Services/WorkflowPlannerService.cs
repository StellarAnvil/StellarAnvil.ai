using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using StellarAnvil.Domain.Entities;
using StellarAnvil.Domain.Services;
using System.Text.Json;

namespace StellarAnvil.Application.Services;

/// <summary>
/// Service for AI-powered workflow selection using Semantic Kernel
/// </summary>
public class WorkflowPlannerService
{
    private readonly IChatClient _chatClient;
    private readonly IWorkflowService _workflowService;

    public WorkflowPlannerService(IChatClient chatClient, IWorkflowService workflowService)
    {
        _chatClient = chatClient;
        _workflowService = workflowService;
    }

    /// <summary>
    /// Select the most appropriate workflow for a given task using AI
    /// </summary>
    public async Task<Workflow> SelectWorkflowForTaskAsync(string taskDescription)
    {
        // Get all available workflows
        var workflows = await _workflowService.GetDefaultWorkflowsAsync();

        if (!workflows.Any())
        {
            throw new InvalidOperationException("No workflows available for selection");
        }

        // If only one workflow, return it
        if (workflows.Count() == 1)
        {
            return workflows.First();
        }

        // Create workflow descriptions for AI decision
        var workflowDescriptions = workflows.Select(w => new
        {
            Name = w.Name,
            Description = w.Description,
            Complexity = GetWorkflowComplexity(w),
            States = GetWorkflowStates(w)
        }).ToList();

        var workflowJson = JsonSerializer.Serialize(workflowDescriptions, new JsonSerializerOptions { WriteIndented = true });

        var plannerPrompt = $@"You are an AI planner for software development workflows.

Task Description: {taskDescription}

Available Workflows:
{workflowJson}

Analyze the task and select the most appropriate workflow based on:
1. Task complexity (simple tasks like ""change font color"" = Simple SDLC)
2. UI/UX requirements (tasks needing design = Full SDLC with UX)
3. Architecture needs (complex features = Standard or Full SDLC)

Respond with ONLY the workflow name (e.g., ""Simple SDLC"", ""Standard SDLC"", or ""Full SDLC"").";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are an expert software development project planner. Analyze tasks and select appropriate workflows."),
            new(ChatRole.User, plannerPrompt)
        };

        // TODO: Fix this when we implement proper AI client integration
        // var response = await _chatClient.GetResponseAsync(messages);
        // var selectedWorkflowName = response.Messages?.FirstOrDefault()?.Text?.Trim();
        var selectedWorkflowName = "Simple SDLC Workflow"; // Temporary hardcode

        // Find the selected workflow
        var selectedWorkflow = workflows.FirstOrDefault(w =>
            string.Equals(w.Name, selectedWorkflowName, StringComparison.OrdinalIgnoreCase));

        // Fallback to rule-based selection if AI selection fails
        if (selectedWorkflow == null)
        {
            selectedWorkflow = SelectWorkflowByRules(taskDescription, workflows.ToList());
        }

        return selectedWorkflow;
    }

    /// <summary>
    /// Rule-based fallback for workflow selection
    /// </summary>
    private Workflow SelectWorkflowByRules(string taskDescription, List<Workflow> workflows)
    {
        var description = taskDescription.ToLower();

        // Simple task indicators
        var simpleIndicators = new[] { "change", "fix", "update", "color", "text", "font", "style", "css" };
        if (simpleIndicators.Any(indicator => description.Contains(indicator)))
        {
            return workflows.FirstOrDefault(w => w.Name == "Simple SDLC") ?? workflows.First();
        }

        // UI/UX task indicators
        var uiIndicators = new[] { "ui", "ux", "design", "interface", "user experience", "layout", "mockup", "wireframe" };
        if (uiIndicators.Any(indicator => description.Contains(indicator)))
        {
            return workflows.FirstOrDefault(w => w.Name == "Full SDLC") ?? workflows.First();
        }

        // Complex feature indicators
        var complexIndicators = new[] { "api", "database", "integration", "architecture", "system", "service", "microservice" };
        if (complexIndicators.Any(indicator => description.Contains(indicator)))
        {
            return workflows.FirstOrDefault(w => w.Name == "Standard SDLC") ?? workflows.First();
        }

        // Default to Standard SDLC
        return workflows.FirstOrDefault(w => w.Name == "Standard SDLC") ?? workflows.First();
    }

    private static string GetWorkflowComplexity(Workflow workflow)
    {
        return workflow.Name switch
        {
            "Simple SDLC" => "Low - for basic tasks and quick fixes",
            "Standard SDLC" => "Medium - for features requiring architecture",
            "Full SDLC" => "High - for complex features with UI/UX design",
            _ => "Unknown"
        };
    }

    private static string GetWorkflowStates(Workflow workflow)
    {
        if (workflow.Transitions?.Any() == true)
        {
            var states = workflow.Transitions
                .OrderBy(t => t.Order)
                .Select(t => t.FromState.ToString())
                .Union(workflow.Transitions.Select(t => t.ToState.ToString()))
                .Distinct()
                .ToList();

            return string.Join(" → ", states);
        }

        return workflow.Name switch
        {
            "Simple SDLC" => "Planning → Requirements → Development → QA → Done",
            "Standard SDLC" => "Planning → Requirements → Architecture → Development → QA → Done",
            "Full SDLC" => "Planning → Requirements → Architecture → UX → Development → QA → Done",
            _ => "Unknown workflow structure"
        };
    }
}