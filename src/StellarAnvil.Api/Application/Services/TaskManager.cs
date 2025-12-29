using StellarAnvil.Api.Application.DTOs;
using StellarAnvil.Api.Application.Helpers;
using StellarAnvil.Api.Application.Results;
using StellarAnvil.Api.Application.Streaming;
using StellarAnvil.Api.Domain.Entities;
using StellarAnvil.Api.Domain.Interfaces;
using ChatMessage = StellarAnvil.Api.Application.DTOs.ChatMessage;

namespace StellarAnvil.Api.Application.Services;

/// <summary>
/// Manages task lifecycle: creation, loading, state transitions, and conversation history.
/// </summary>
public class TaskManager : ITaskManager
{
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<TaskManager> _logger;

    public TaskManager(ITaskRepository taskRepository, ILogger<TaskManager> logger)
    {
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task<AgentTask> ResolveTaskAsync(ChatCompletionRequest request)
    {
        var taskId = TaskIdHelper.ExtractTaskId(request.Messages);
        
        if (taskId == null)
        {
            return await CreateNewTaskAsync(request);
        }
        
        return await LoadExistingTaskAsync(taskId, request);
    }

    public async Task UpdateForToolCallsAsync(AgentTask task, List<RequestedToolCall> toolCalls, string? agentId)
    {
        // Store pending tool calls for validation on next request
        task.PendingToolCalls = toolCalls
            .Select(tc => new PendingToolCall(tc.CallId, tc.FunctionName, tc.Arguments))
            .ToList();
        task.LastActiveAgent = agentId;
        task.State = TaskState.AwaitingToolResult;
        
        // Store the assistant message with tool calls
        task.UserMessages.Add(new ChatMessage
        {
            Role = "assistant",
            Content = null,
            ToolCalls = OpenAiStreamWriter.ToToolCallDtos(toolCalls)
        });
        
        await _taskRepository.UpdateTaskAsync(task);
        
        _logger.LogInformation("Task {TaskId}: Updated for {Count} tool calls, state={State}", 
            task.TaskId, toolCalls.Count, task.State);
    }

    public async Task UpdateForTextResponseAsync(AgentTask task, string response, bool isComplete)
    {
        task.State = isComplete ? TaskState.Completed : TaskState.AwaitingUser;
        
        // Store the assistant response with task ID
        var responseWithTaskId = TaskIdHelper.AppendTaskId(response, task.TaskId);
        task.UserMessages.Add(new ChatMessage
        {
            Role = "assistant",
            Content = responseWithTaskId
        });
        
        await _taskRepository.UpdateTaskAsync(task);
        
        _logger.LogInformation("Task {TaskId}: Updated for text response, state={State}", 
            task.TaskId, task.State);
    }

    private async Task<AgentTask> CreateNewTaskAsync(ChatCompletionRequest request)
    {
        var task = await _taskRepository.CreateTaskAsync();
        _logger.LogInformation("Created new task {TaskId}", task.TaskId);
        
        // Store the initial user message
        var userMessage = request.Messages.LastOrDefault(m => 
            m.Role.Equals("user", StringComparison.OrdinalIgnoreCase));
        if (userMessage != null)
        {
            task.UserMessages.Add(userMessage);
        }
        
        // Store tools from the request for agent function calling
        task.Tools = request.Tools;
        task.State = TaskState.Working;
        
        return task;
    }

    private async Task<AgentTask> LoadExistingTaskAsync(string taskId, ChatCompletionRequest request)
    {
        var task = await _taskRepository.GetTaskAsync(taskId) 
            ?? throw new InvalidOperationException($"Task {taskId} not found");
        
        var hasToolResults = request.Messages.Any(m => 
            m.Role.Equals("tool", StringComparison.OrdinalIgnoreCase));
        
        _logger.LogInformation("Resuming task {TaskId} in state {State}, HasToolResults={HasToolResults}", 
            task.TaskId, task.State, hasToolResults);
        
        // IMPORTANT: Cursor sends the FULL conversation history in each request.
        // Use request.Messages as the authoritative source to avoid duplicate accumulation.
        var conversationMessages = request.Messages
            .Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        task.UserMessages = conversationMessages;
        _logger.LogInformation("Task {TaskId}: Using {Count} messages from request as conversation history", 
            task.TaskId, conversationMessages.Count);
        
        if (hasToolResults && task.State == TaskState.AwaitingToolResult)
        {
            LogToolResults(task.TaskId, request.Messages);
        }
        else
        {
            // Normal user message continuation - clear tool call state
            task.PendingToolCalls = null;
            task.LastActiveAgent = null;
        }
        
        // Update tools if provided in continuation request
        if (request.Tools != null)
        {
            task.Tools = request.Tools;
        }
        
        return task;
    }

    private void LogToolResults(string taskId, List<ChatMessage> messages)
    {
        var toolResults = messages.Where(m => 
            m.Role.Equals("tool", StringComparison.OrdinalIgnoreCase));
        
        foreach (var toolResult in toolResults)
        {
            _logger.LogInformation("Task {TaskId}: Has tool result for {ToolCallId}", 
                taskId, toolResult.ToolCallId);
        }
    }
}

