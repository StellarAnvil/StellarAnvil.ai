using System.Runtime.CompilerServices;
using StellarAnvil.Api.Application.DTOs;
using StellarAnvil.Api.Application.Services;
using StellarAnvil.Api.Application.Streaming;
using StellarAnvil.Api.Domain.Entities;

namespace StellarAnvil.Api.Application.UseCases;

/// <summary>
/// Orchestrates the chat completion workflow.
/// Acts as a thin coordinator delegating to specialized services.
/// </summary>
public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly ITaskManager _taskManager;
    private readonly IWorkflowExecutor _workflowExecutor;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        ITaskManager taskManager,
        IWorkflowExecutor workflowExecutor,
        ILogger<AgentOrchestrator> logger)
    {
        _taskManager = taskManager;
        _workflowExecutor = workflowExecutor;
        _logger = logger;
    }

    public async IAsyncEnumerable<ChatCompletionChunk> ProcessAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1. Resolve task (create new or load existing)
        var task = await _taskManager.ResolveTaskAsync(request);
        task.State = TaskState.Working;
        
        // 2. Execute workflow
        var result = await _workflowExecutor.ExecuteAsync(task, cancellationToken);
        
        // 3. Handle result and stream response
        var model = request.Model ?? "gpt-5-nano";
        
        if (result.HasToolCalls)
        {
            _logger.LogInformation("Task {TaskId}: Streaming {Count} tool calls", 
                task.TaskId, result.ToolCalls.Count);
            
            await _taskManager.UpdateForToolCallsAsync(task, result.ToolCalls, result.ToolCallAgent);
            
            await foreach (var chunk in OpenAiStreamWriter.StreamToolCallsAsync(
                result.ToolCalls, task.TaskId, model, cancellationToken))
            {
                yield return chunk;
            }
        }
        else
        {
            _logger.LogInformation("Task {TaskId}: Streaming text response, isComplete={IsComplete}", 
                task.TaskId, result.IsComplete);
            
            await _taskManager.UpdateForTextResponseAsync(task, result.Response, result.IsComplete);
            
            await foreach (var chunk in OpenAiStreamWriter.StreamResponseWithTaskIdAsync(
                result.Response, task.TaskId, model, cancellationToken))
            {
                yield return chunk;
            }
        }
    }
}
