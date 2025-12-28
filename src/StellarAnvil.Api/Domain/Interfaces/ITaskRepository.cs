using StellarAnvil.Api.Domain.Entities;

namespace StellarAnvil.Api.Domain.Interfaces;

public interface ITaskRepository
{
    /// <summary>
    /// Creates a new task and returns it with a generated TaskId
    /// </summary>
    Task<AgentTask> CreateTaskAsync();
    
    /// <summary>
    /// Retrieves a task by its ID
    /// </summary>
    Task<AgentTask?> GetTaskAsync(string taskId);
    
    /// <summary>
    /// Updates an existing task
    /// </summary>
    Task UpdateTaskAsync(AgentTask task);
}

