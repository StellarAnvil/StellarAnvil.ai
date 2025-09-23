using StellarAnvil.Application.DTOs;

namespace StellarAnvil.Application.Services;

public interface ITaskApplicationService
{
    Task<IEnumerable<TaskDto>> GetAllAsync();
    Task<TaskDto?> GetByIdAsync(Guid id);
    Task<TaskDto?> GetByTaskNumberAsync(int taskNumber);
    Task<TaskDto> CreateAsync(CreateTaskDto createDto);
    Task<TaskDto?> UpdateAsync(Guid id, UpdateTaskDto updateDto);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> AssignTaskAsync(Guid taskId, Guid teamMemberId);
    Task<WorkflowProgress> GetWorkflowProgressAsync(Guid id);
    Task<bool> TransitionWorkflowAsync(Guid id, WorkflowTrigger trigger, Guid? executedBy = null);
    Task<List<WorkflowTrigger>> GetAvailableTriggersAsync(Guid id);
}
