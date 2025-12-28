using System.Collections.Concurrent;
using StellarAnvil.Api.Domain.Entities;
using StellarAnvil.Api.Domain.Interfaces;

namespace StellarAnvil.Api.Infrastructure.Persistence;

public class InMemoryTaskRepository : ITaskRepository
{
    private readonly ConcurrentDictionary<string, AgentTask> _tasks = new();
    
    public Task<AgentTask> CreateTaskAsync()
    {
        var taskId = GenerateTaskId();
        var task = new AgentTask
        {
            TaskId = taskId,
            State = TaskState.Created,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        _tasks[taskId] = task;
        return Task.FromResult(task);
    }
    
    public Task<AgentTask?> GetTaskAsync(string taskId)
    {
        _tasks.TryGetValue(taskId, out var task);
        return Task.FromResult(task);
    }
    
    public Task UpdateTaskAsync(AgentTask task)
    {
        task.UpdatedAt = DateTime.UtcNow;
        _tasks[task.TaskId] = task;
        return Task.CompletedTask;
    }
    
    private static string GenerateTaskId()
    {
        // Generate a short, URL-safe task ID
        return Guid.NewGuid().ToString("N")[..8];
    }
}

