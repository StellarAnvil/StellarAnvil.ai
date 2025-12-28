using StellarAnvil.Api.Models.Task;

namespace StellarAnvil.Api.Services;

public interface ITaskStore
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
    
    /// <summary>
    /// Checks if a task exists
    /// </summary>
    Task<bool> ExistsAsync(string taskId);
}

