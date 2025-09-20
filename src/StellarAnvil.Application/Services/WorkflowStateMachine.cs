using Stateless;
using StellarAnvil.Domain.Entities;
using StellarAnvil.Domain.Enums;
using StellarAnvil.Domain.Services;
using StellarAnvil.Infrastructure.Repositories;

namespace StellarAnvil.Application.Services;

/// <summary>
/// Workflow state machine using Stateless library
/// </summary>
public class WorkflowStateMachine
{
    private readonly IRepository<Domain.Entities.Task> _taskRepository;
    private readonly IRepository<TaskHistory> _taskHistoryRepository;
    private readonly IRepository<Workflow> _workflowRepository;
    private readonly ITeamMemberService _teamMemberService;
    private readonly AutoGenCollaborationService _collaborationService;

    public WorkflowStateMachine(
        IRepository<Domain.Entities.Task> taskRepository,
        IRepository<TaskHistory> taskHistoryRepository,
        IRepository<Workflow> workflowRepository,
        ITeamMemberService teamMemberService,
        AutoGenCollaborationService collaborationService)
    {
        _taskRepository = taskRepository;
        _taskHistoryRepository = taskHistoryRepository;
        _workflowRepository = workflowRepository;
        _teamMemberService = teamMemberService;
        _collaborationService = collaborationService;
    }

    /// <summary>
    /// Create a state machine for a specific task
    /// </summary>
    public async System.Threading.Tasks.Task<StateMachine<WorkflowState, WorkflowTrigger>> CreateStateMachineAsync(Guid taskId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            throw new ArgumentException($"Task with ID {taskId} not found");

        var workflow = await _workflowRepository.GetByIdAsync(task.WorkflowId);
        if (workflow == null)
            throw new ArgumentException($"Workflow with ID {task.WorkflowId} not found");

        var stateMachine = new StateMachine<WorkflowState, WorkflowTrigger>(
            () => task.CurrentState,
            state => 
            {
                task.CurrentState = state;
                task.UpdatedAt = DateTime.UtcNow;
            });

        // Configure transitions based on workflow
        await ConfigureWorkflowTransitionsAsync(stateMachine, workflow, taskId);

        return stateMachine;
    }

    private async System.Threading.Tasks.Task ConfigureWorkflowTransitionsAsync(
        StateMachine<WorkflowState, WorkflowTrigger> stateMachine,
        Workflow workflow,
        Guid taskId)
    {
        // Configure Planning state
        stateMachine.Configure(WorkflowState.Planning)
            .Permit(WorkflowTrigger.StartRequirementsAnalysis, WorkflowState.RequirementsAnalysis);

        // Configure Requirements Analysis state
        stateMachine.Configure(WorkflowState.RequirementsAnalysis)
            .Permit(WorkflowTrigger.StartArchitecturalDesign, WorkflowState.ArchitecturalDesign)
            .Permit(WorkflowTrigger.StartDevelopment, WorkflowState.Development);

        // Configure Architectural Design state
        stateMachine.Configure(WorkflowState.ArchitecturalDesign)
            .Permit(WorkflowTrigger.StartUXDesign, WorkflowState.UXDesign)
            .Permit(WorkflowTrigger.StartDevelopment, WorkflowState.Development);

        // Configure UX Design state
        stateMachine.Configure(WorkflowState.UXDesign)
            .Permit(WorkflowTrigger.StartDevelopment, WorkflowState.Development);

        // Configure Development state
        stateMachine.Configure(WorkflowState.Development)
            .Permit(WorkflowTrigger.StartQualityAssurance, WorkflowState.QualityAssurance);

        // Configure Quality Assurance state
        stateMachine.Configure(WorkflowState.QualityAssurance)
            .Permit(WorkflowTrigger.StartSecurityReview, WorkflowState.SecurityReview)
            .Permit(WorkflowTrigger.Complete, WorkflowState.Completed);

        // Configure Security Review state
        stateMachine.Configure(WorkflowState.SecurityReview)
            .Permit(WorkflowTrigger.Complete, WorkflowState.Completed);

        // Configure Completed state (terminal)
        stateMachine.Configure(WorkflowState.Completed);
    }

    private async System.Threading.Tasks.Task AssignToRoleAsync(Guid taskId, TeamMemberRole role)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null) return;

        // Unassign current team member if any
        if (task.AssigneeId.HasValue)
        {
            await _teamMemberService.UnassignTaskAsync(task.AssigneeId.Value);
        }

        // Use AutoGen collaboration to assign and manage the work
        var collaborationResult = await _collaborationService.CollaborateAsync(
            taskId, role, task.Description, "Starting work on this task...");

        // Log the assignment
        await _taskHistoryRepository.AddAsync(new TaskHistory
        {
            TaskId = taskId,
            TeamMemberId = collaborationResult.AssignedMember?.Id,
            FromState = task.CurrentState,
            ToState = task.CurrentState, // Same state, just assignment change
            Action = $"Assigned to {collaborationResult.AssignedMember?.Name} ({role})",
            Notes = collaborationResult.Message
        });
    }

    private async System.Threading.Tasks.Task CompleteTaskAsync(Guid taskId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null) return;

        // Unassign team member
        if (task.AssigneeId.HasValue)
        {
            await _teamMemberService.UnassignTaskAsync(task.AssigneeId.Value);
        }

        // Log completion
        await _taskHistoryRepository.AddAsync(new TaskHistory
        {
            TaskId = taskId,
            TeamMemberId = task.AssigneeId,
            FromState = task.CurrentState,
            ToState = WorkflowState.Completed,
            Action = "Task completed",
            Notes = "Task has been successfully completed and moved to final state"
        });
    }

    /// <summary>
    /// Get available triggers for current state
    /// </summary>
    public async System.Threading.Tasks.Task<List<WorkflowTrigger>> GetAvailableTriggersAsync(Guid taskId)
    {
        var stateMachine = await CreateStateMachineAsync(taskId);
        return stateMachine.PermittedTriggers.ToList();
    }

    /// <summary>
    /// Execute a state transition
    /// </summary>
    public async System.Threading.Tasks.Task<bool> ExecuteTransitionAsync(Guid taskId, WorkflowTrigger trigger, Guid? executedBy = null)
    {
        try
        {
            var stateMachine = await CreateStateMachineAsync(taskId);
            
            if (!stateMachine.CanFire(trigger))
                return false;

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null) return false;

            var fromState = task.CurrentState;

            // Execute the transition
            await stateMachine.FireAsync(trigger);

            // Update the task in database
            await _taskRepository.UpdateAsync(task);

            // Log the transition
            await _taskHistoryRepository.AddAsync(new TaskHistory
            {
                TaskId = taskId,
                TeamMemberId = executedBy,
                FromState = fromState,
                ToState = task.CurrentState,
                Action = $"Triggered: {trigger}",
                Notes = $"State transition from {fromState} to {task.CurrentState}"
            });

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Get workflow progress summary
    /// </summary>
    public async System.Threading.Tasks.Task<WorkflowProgress> GetWorkflowProgressAsync(Guid taskId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            throw new ArgumentException($"Task with ID {taskId} not found");

        var workflow = await _workflowRepository.GetByIdAsync(task.WorkflowId);
        if (workflow == null)
            throw new ArgumentException($"Workflow with ID {task.WorkflowId} not found");

        var history = await _taskHistoryRepository.FindAsync(h => h.TaskId == taskId);
        var availableTriggers = await GetAvailableTriggersAsync(taskId);

        return new WorkflowProgress
        {
            TaskId = taskId,
            WorkflowName = workflow.Name,
            CurrentState = task.CurrentState,
            AvailableTriggers = availableTriggers,
            History = history.OrderBy(h => h.CreatedAt).ToList(),
            IsCompleted = task.CurrentState == WorkflowState.Completed,
            AssigneeId = task.AssigneeId
        };
    }
}

/// <summary>
/// Workflow triggers for state transitions
/// </summary>
public enum WorkflowTrigger
{
    StartRequirementsAnalysis,
    StartArchitecturalDesign,
    StartUXDesign,
    StartDevelopment,
    StartQualityAssurance,
    StartSecurityReview,
    Complete,
    Reject,
    RequestChanges
}

/// <summary>
/// Workflow progress information
/// </summary>
public class WorkflowProgress
{
    public Guid TaskId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public WorkflowState CurrentState { get; set; }
    public List<WorkflowTrigger> AvailableTriggers { get; set; } = new();
    public List<TaskHistory> History { get; set; } = new();
    public bool IsCompleted { get; set; }
    public Guid? AssigneeId { get; set; }
}
