using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using StellarAnvil.Domain.Entities;
using StellarAnvil.Domain.Enums;
using StellarAnvil.Domain.Services;
using System.Text.Json;

namespace StellarAnvil.Application.Services;

/// <summary>
/// AutoGen-style multi-agent collaboration service
/// </summary>
public class AutoGenCollaborationService
{
    private readonly IAIClientService _aiClientService;
    private readonly ITeamMemberService _teamMemberService;
    private readonly IWorkflowService _workflowService;

    public AutoGenCollaborationService(
        IAIClientService aiClientService,
        ITeamMemberService teamMemberService,
        IWorkflowService workflowService)
    {
        _aiClientService = aiClientService;
        _teamMemberService = teamMemberService;
        _workflowService = workflowService;
    }

    /// <summary>
    /// Orchestrate collaboration between junior and senior team members
    /// </summary>
    public async Task<CollaborationResult> CollaborateAsync(
        Guid taskId,
        TeamMemberRole role,
        string taskDescription,
        string initialWork)
    {
        // Get available team members for this role
        var juniorMember = await _teamMemberService.GetAvailableTeamMemberAsync(role, TeamMemberGrade.Junior);
        var seniorMember = await _teamMemberService.GetAvailableTeamMemberAsync(role, TeamMemberGrade.Senior);

        if (juniorMember == null && seniorMember == null)
        {
            return new CollaborationResult
            {
                Success = false,
                Message = $"No available team members found for role {role}",
                FinalOutput = initialWork
            };
        }

        // If only one member available, assign directly
        if (juniorMember == null || seniorMember == null)
        {
            var availableMember = juniorMember ?? seniorMember;
            await _teamMemberService.AssignTaskAsync(availableMember!.Id, taskId);
            
            return new CollaborationResult
            {
                Success = true,
                Message = $"Task assigned to {availableMember.Name} ({availableMember.Grade} {availableMember.Role})",
                AssignedMember = availableMember,
                FinalOutput = initialWork
            };
        }

        // Conduct junior-senior collaboration
        var collaborationResult = await ConductJuniorSeniorCollaborationAsync(
            juniorMember, seniorMember, taskDescription, initialWork);

        // Assign task to the junior member (they do the work with senior guidance)
        await _teamMemberService.AssignTaskAsync(juniorMember.Id, taskId);

        return new CollaborationResult
        {
            Success = true,
            Message = $"Collaboration completed between {juniorMember.Name} (Junior) and {seniorMember.Name} (Senior)",
            AssignedMember = juniorMember,
            ReviewingMember = seniorMember,
            FinalOutput = collaborationResult.FinalOutput,
            CollaborationHistory = collaborationResult.Messages
        };
    }

    private async Task<CollaborationSession> ConductJuniorSeniorCollaborationAsync(
        TeamMember juniorMember,
        TeamMember seniorMember,
        string taskDescription,
        string initialWork)
    {
        var messages = new List<CollaborationMessage>();
        var currentWork = initialWork;
        var maxIterations = 3;
        var iteration = 0;

        // Use system prompts from team members
        var juniorPrompt = !string.IsNullOrEmpty(juniorMember.SystemPrompt)
            ? juniorMember.SystemPrompt
            : "You are a junior team member. Work on tasks and incorporate feedback from senior members.";
        var seniorPrompt = !string.IsNullOrEmpty(seniorMember.SystemPrompt)
            ? seniorMember.SystemPrompt
            : "You are a senior team member. Review work from junior members and provide constructive feedback.";

        while (iteration < maxIterations)
        {
            iteration++;

            // Junior works on the task
            var juniorResponse = await GetAgentResponse(
                juniorMember,
                juniorPrompt,
                $"Task: {taskDescription}\n\nCurrent work: {currentWork}\n\nPlease work on this task and provide your implementation or analysis.");

            messages.Add(new CollaborationMessage
            {
                Sender = juniorMember.Name,
                Role = "Junior",
                Content = juniorResponse,
                Timestamp = DateTime.UtcNow
            });

            currentWork = juniorResponse;

            // Senior reviews the work
            var seniorReviewPrompt = $"Task: {taskDescription}\n\nJunior's work: {currentWork}\n\nPlease review this work and provide feedback. If it's good enough, say 'APPROVED'. If it needs improvement, provide specific feedback and suggestions.";
            
            var seniorResponse = await GetAgentResponse(
                seniorMember,
                seniorPrompt,
                seniorReviewPrompt);

            messages.Add(new CollaborationMessage
            {
                Sender = seniorMember.Name,
                Role = "Senior Reviewer",
                Content = seniorResponse,
                Timestamp = DateTime.UtcNow
            });

            // Check if senior approved the work
            if (seniorResponse.Contains("APPROVED", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            // If not approved, junior incorporates feedback
            if (iteration < maxIterations)
            {
                var improvementPrompt = $"Task: {taskDescription}\n\nYour previous work: {currentWork}\n\nSenior feedback: {seniorResponse}\n\nPlease improve your work based on the feedback.";
                
                var improvedWork = await GetAgentResponse(
                    juniorMember,
                    juniorPrompt,
                    improvementPrompt);

                currentWork = improvedWork;

                messages.Add(new CollaborationMessage
                {
                    Sender = juniorMember.Name,
                    Role = "Junior (Revision)",
                    Content = improvedWork,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        return new CollaborationSession
        {
            FinalOutput = currentWork,
            Messages = messages,
            IterationsCompleted = iteration
        };
    }

    private async Task<string> GetAgentResponse(TeamMember member, string systemPrompt, string userPrompt)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(Microsoft.Extensions.AI.ChatRole.System, systemPrompt),
            new(Microsoft.Extensions.AI.ChatRole.User, userPrompt)
        };

        try
        {
            var chatClient = await _aiClientService.GetClientForModelAsync(member.Model ?? "deepseek-r1");
            var response = await chatClient.GetResponseAsync(messages);
            return response.Messages?.FirstOrDefault()?.Text ?? "No response generated";
        }
        catch (Exception)
        {
            // Fallback to a helpful response if AI client fails
            return $"I'm {member.Name} ({member.Role} - {member.Grade}). I'm working on this task and will provide my analysis shortly.";
        }
    }


    /// <summary>
    /// Get confirmation from lead or human team member
    /// </summary>
    public async Task<bool> GetLeadConfirmationAsync(
        Guid taskId,
        TeamMemberRole role,
        string workSummary)
    {
        // Try to find a Lead for this role
        var leadMember = await _teamMemberService.GetAvailableTeamMemberAsync(role, TeamMemberGrade.Lead);
        
        if (leadMember != null && leadMember.Type == TeamMemberType.AI)
        {
            var leadPrompt = !string.IsNullOrEmpty(leadMember.SystemPrompt)
                ? leadMember.SystemPrompt
                : "You are a lead team member. Review work and make final approval decisions.";
            var confirmationPrompt = $"Please review this work and decide if it's ready to move to the next phase:\n\n{workSummary}\n\nRespond with 'YES' if approved or 'NO' with specific concerns if not approved.";

            var response = await GetAgentResponse(leadMember, leadPrompt, confirmationPrompt);
            return response.Trim().StartsWith("YES", StringComparison.OrdinalIgnoreCase);
        }

        // If no AI lead available, require human confirmation
        return false; // This will trigger a request for human confirmation in the chat
    }
}

public class CollaborationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public TeamMember? AssignedMember { get; set; }
    public TeamMember? ReviewingMember { get; set; }
    public string FinalOutput { get; set; } = string.Empty;
    public List<CollaborationMessage> CollaborationHistory { get; set; } = new();
}

public class CollaborationSession
{
    public string FinalOutput { get; set; } = string.Empty;
    public List<CollaborationMessage> Messages { get; set; } = new();
    public int IterationsCompleted { get; set; }
}

public class CollaborationMessage
{
    public string Sender { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
