using AutoMapper;
using Microsoft.EntityFrameworkCore;
using StellarAnvil.Application.DTOs;
using StellarAnvil.Domain.Entities;
using StellarAnvil.Domain.Enums;
using StellarAnvil.Domain.Services;
using StellarAnvil.Infrastructure.Data;
using StellarAnvil.Infrastructure.Repositories;

namespace StellarAnvil.Application.Services;

public class TaskApplicationService : ITaskApplicationService
{
    private readonly IRepository<Domain.Entities.Task> _taskRepository;
    private readonly IWorkflowService _workflowService;
    private readonly WorkflowStateMachine _stateMachine;
    private readonly StellarAnvilDbContext _context;
    private readonly IMapper _mapper;

    public TaskApplicationService(
        IRepository<Domain.Entities.Task> taskRepository,
        IWorkflowService workflowService,
        WorkflowStateMachine stateMachine,
        StellarAnvilDbContext context,
        IMapper mapper)
    {
        _taskRepository = taskRepository;
        _workflowService = workflowService;
        _stateMachine = stateMachine;
        _context = context;
        _mapper = mapper;
    }

    public async Task<IEnumerable<TaskDto>> GetAllAsync()
    {
        var tasks = await _context.Tasks
            .Include(t => t.Assignee)
            .Include(t => t.Workflow)
            .ToListAsync();
        
        return _mapper.Map<IEnumerable<TaskDto>>(tasks);
    }

    public async Task<TaskDto?> GetByIdAsync(Guid id)
    {
        var task = await _context.Tasks
            .Include(t => t.Assignee)
            .Include(t => t.Workflow)
            .FirstOrDefaultAsync(t => t.Id == id);
        
        return task != null ? _mapper.Map<TaskDto>(task) : null;
    }

    public async Task<TaskDto> CreateAsync(CreateTaskDto createDto)
    {
        // Select appropriate workflow based on task description
        var workflow = await _workflowService.SelectWorkflowForTaskAsync(createDto.Description);

        var task = new Domain.Entities.Task
        {
            Description = createDto.Description,
            CurrentState = WorkflowState.Planning,
            WorkflowId = workflow.Id
        };

        var createdTask = await _taskRepository.AddAsync(task);

        // Start the workflow by assigning to Product Owner
        await _stateMachine.ExecuteTransitionAsync(createdTask.Id, WorkflowTrigger.StartRequirementsAnalysis);

        // Return the updated task with assignee and workflow info
        var taskWithDetails = await _context.Tasks
            .Include(t => t.Assignee)
            .Include(t => t.Workflow)
            .FirstOrDefaultAsync(t => t.Id == createdTask.Id);

        return _mapper.Map<TaskDto>(taskWithDetails);
    }

    public async Task<TaskDto?> UpdateAsync(Guid id, UpdateTaskDto updateDto)
    {
        var existingTask = await _taskRepository.GetByIdAsync(id);
        if (existingTask == null)
            return null;

        _mapper.Map(updateDto, existingTask);
        existingTask.UpdatedAt = DateTime.UtcNow;

        var updatedTask = await _taskRepository.UpdateAsync(existingTask);

        // Return the updated task with assignee and workflow info
        var taskWithDetails = await _context.Tasks
            .Include(t => t.Assignee)
            .Include(t => t.Workflow)
            .FirstOrDefaultAsync(t => t.Id == updatedTask.Id);

        return _mapper.Map<TaskDto>(taskWithDetails);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var task = await _taskRepository.GetByIdAsync(id);
        if (task == null)
            return false;

        // Unassign team member if assigned
        if (task.AssigneeId.HasValue)
        {
            var teamMember = await _context.TeamMembers.FindAsync(task.AssigneeId.Value);
            if (teamMember != null)
            {
                teamMember.CurrentTaskId = null;
                teamMember.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _taskRepository.DeleteAsync(task);
        return true;
    }

    public async Task<WorkflowProgress> GetWorkflowProgressAsync(Guid id)
    {
        return await _stateMachine.GetWorkflowProgressAsync(id);
    }

    public async Task<bool> TransitionWorkflowAsync(Guid id, WorkflowTrigger trigger, Guid? executedBy = null)
    {
        return await _stateMachine.ExecuteTransitionAsync(id, trigger, executedBy);
    }

    public async Task<List<WorkflowTrigger>> GetAvailableTriggersAsync(Guid id)
    {
        return await _stateMachine.GetAvailableTriggersAsync(id);
    }
}
