using StellarAnvil.Domain.Entities;
using StellarAnvil.Domain.Enums;

namespace StellarAnvil.Domain.Services;

public interface IWorkflowService
{
    Task<Workflow> SelectWorkflowForTaskAsync(string taskDescription);
    Task<bool> CanTransitionAsync(Guid taskId, WorkflowState toState, Guid teamMemberId);
    Task<bool> TransitionTaskAsync(Guid taskId, WorkflowState toState, Guid teamMemberId, string? notes = null);
    Task<TeamMember?> GetNextAssigneeAsync(Guid taskId, WorkflowState currentState);
    Task<bool> IsConfirmationMessageAsync(string message);
    Task<IEnumerable<Workflow>> GetDefaultWorkflowsAsync();
}
