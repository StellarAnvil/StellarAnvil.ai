using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using StellarAnvil.Domain.Entities;
using StellarAnvil.Domain.Enums;
using StellarAnvil.Domain.Services;
using StellarAnvil.Infrastructure.Data;
using StellarAnvil.Infrastructure.Repositories;
using System.Text.RegularExpressions;

namespace StellarAnvil.Infrastructure.Services;

public class WorkflowService : IWorkflowService
{
    private readonly StellarAnvilDbContext _context;
    private readonly IRepository<Workflow> _workflowRepository;
    private readonly IRepository<Domain.Entities.Task> _taskRepository;
    private readonly IRepository<TaskHistory> _taskHistoryRepository;
    private readonly IRepository<TeamMember> _teamMemberRepository;
    private readonly IChatClient _chatClient;

    public WorkflowService(
        StellarAnvilDbContext context,
        IRepository<Workflow> workflowRepository,
        IRepository<Domain.Entities.Task> taskRepository,
        IRepository<TaskHistory> taskHistoryRepository,
        IRepository<TeamMember> teamMemberRepository,
        IChatClient chatClient)
    {
        _context = context;
        _workflowRepository = workflowRepository;
        _taskRepository = taskRepository;
        _taskHistoryRepository = taskHistoryRepository;
        _teamMemberRepository = teamMemberRepository;
        _chatClient = chatClient;
    }

    public async Task<Workflow> SelectWorkflowForTaskAsync(string taskDescription)
    {
        var workflows = await _context.Workflows
            .Include(w => w.Transitions)
            .Where(w => w.IsDefault)
            .ToListAsync();

        var workflowNames = string.Join(", ", workflows.Select(w => w.Name));
        
        var prompt = $@"Given the task description: '{taskDescription}' and available workflows: [{workflowNames}], 
                       choose the most appropriate workflow. Consider:
                       - Simple SDLC Workflow: for small changes, bug fixes, minor features
                       - Standard SDLC Workflow: for medium features without UI changes
                       - Full SDLC Workflow: for complex features requiring UI/UX design
                       
                       Respond with only the workflow name.";

        // TODO: Fix this when we implement proper AI client integration
        // var response = await _chatClient.GetResponseAsync(prompt);
        // var selectedWorkflowName = response.Message?.Text?.Trim();
        var selectedWorkflowName = "Simple SDLC Workflow"; // Temporary hardcode

        var selectedWorkflow = workflows.FirstOrDefault(w => 
            w.Name.Contains(selectedWorkflowName ?? "", StringComparison.OrdinalIgnoreCase));

        return selectedWorkflow ?? workflows.First(w => w.Name.Contains("Simple"));
    }

    public async Task<bool> CanTransitionAsync(Guid taskId, WorkflowState toState, Guid teamMemberId)
    {
        var task = await _context.Tasks
            .Include(t => t.Workflow)
            .ThenInclude(w => w.Transitions)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) return false;

        var teamMember = await _teamMemberRepository.GetByIdAsync(teamMemberId);
        if (teamMember == null) return false;

        var transition = task.Workflow.Transitions
            .FirstOrDefault(t => t.FromState == task.CurrentState && t.ToState == toState);

        if (transition == null) return false;

        // Check if team member has the required role
        return teamMember.Role == transition.RequiredRole;
    }

    public async Task<bool> TransitionTaskAsync(Guid taskId, WorkflowState toState, Guid teamMemberId, string? notes = null)
    {
        if (!await CanTransitionAsync(taskId, toState, teamMemberId))
            return false;

        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null) return false;

        var fromState = task.CurrentState;
        task.CurrentState = toState;
        task.UpdatedAt = DateTime.UtcNow;

        await _taskRepository.UpdateAsync(task);

        // Create history entry
        var history = new TaskHistory
        {
            TaskId = taskId,
            TeamMemberId = teamMemberId,
            FromState = fromState,
            ToState = toState,
            Action = $"Transitioned from {fromState} to {toState}",
            Notes = notes
        };

        await _taskHistoryRepository.AddAsync(history);

        return true;
    }

    public async Task<TeamMember?> GetNextAssigneeAsync(Guid taskId, WorkflowState currentState)
    {
        var task = await _context.Tasks
            .Include(t => t.Workflow)
            .ThenInclude(w => w.Transitions)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) return null;

        var nextTransition = task.Workflow.Transitions
            .Where(t => t.FromState == currentState)
            .OrderBy(t => t.Order)
            .FirstOrDefault();

        if (nextTransition == null) return null;

        // Find available team member with required role, preferring Junior, then Senior, then Lead
        var availableMembers = await _context.TeamMembers
            .Where(tm => tm.Role == nextTransition.RequiredRole && tm.CurrentTaskId == null)
            .OrderBy(tm => tm.Grade)
            .ToListAsync();

        return availableMembers.FirstOrDefault();
    }

    public async Task<bool> IsConfirmationMessageAsync(string message)
    {
        // Use flexible regex patterns for confirmation
        var confirmationPatterns = new[]
        {
            @"\b(yes|yep|yeah|ok|okay|sure|approve|approved|confirm|confirmed|good|happy|proceed|continue)\b",
            @"\b(i am happy|looks good|sounds good|go ahead|let's do it|let's proceed)\b"
        };

        foreach (var pattern in confirmationPatterns)
        {
            if (Regex.IsMatch(message, pattern, RegexOptions.IgnoreCase))
                return true;
        }

        // Use AI as fallback for more complex confirmation detection
        var prompt = $@"Is the following message a confirmation or approval? 
                       Message: '{message}'
                       
                       Respond with only 'YES' or 'NO'.";

        // TODO: Fix this when we implement proper AI client integration
        // var response = await _chatClient.GetResponseAsync(prompt);
        // var result = response.Message?.Text?.Trim().ToUpperInvariant();
        var result = "YES"; // Temporary hardcode

        return result == "YES";
    }

    public async Task<IEnumerable<Workflow>> GetDefaultWorkflowsAsync()
    {
        return await _context.Workflows
            .Include(w => w.Transitions)
            .Where(w => w.IsDefault)
            .ToListAsync();
    }
}
