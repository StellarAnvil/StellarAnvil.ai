using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using StellarAnvil.Api.Helpers;
using StellarAnvil.Api.Models.OpenAI;
using StellarAnvil.Api.Models.Task;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatMessage = StellarAnvil.Api.Models.OpenAI.ChatMessage;

namespace StellarAnvil.Api.Services;

public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IDeliberationWorkflow _deliberationWorkflow;
    private readonly ITaskStore _taskStore;
    private readonly IAgentRegistry _agentRegistry;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        IDeliberationWorkflow deliberationWorkflow,
        ITaskStore taskStore,
        IAgentRegistry agentRegistry,
        ILogger<AgentOrchestrator> logger)
    {
        _deliberationWorkflow = deliberationWorkflow;
        _taskStore = taskStore;
        _agentRegistry = agentRegistry;
        _logger = logger;
    }

    public async IAsyncEnumerable<ChatCompletionChunk> ProcessAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1. Detect if this is fresh or continuation
        var taskId = TaskIdHelper.ExtractTaskId(request.Messages);
        AgentTask task;
        
        if (taskId == null)
        {
            // Fresh chat - create new task
            task = await _taskStore.CreateTaskAsync();
            _logger.LogInformation("Created new task {TaskId}", task.TaskId);
            
            // Store the initial user message
            var userMessage = request.Messages.LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase));
            if (userMessage != null)
            {
                task.UserMessages.Add(userMessage);
            }
            
            // Start with BA phase
            task.State = TaskState.BA_Working;
            task.CurrentPhase = TaskPhase.BA;
            task.CurrentAgent = _agentRegistry.GetJuniorAgent(TaskPhase.BA);
        }
        else
        {
            // Continuation - load existing task
            task = await _taskStore.GetTaskAsync(taskId) 
                ?? throw new InvalidOperationException($"Task {taskId} not found");
            _logger.LogInformation("Resuming task {TaskId} in state {State}", task.TaskId, task.State);
            
            // Add the new user message
            var userMessage = request.Messages.LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase));
            if (userMessage != null)
            {
                task.UserMessages.Add(userMessage);
            }
            
            // Handle user response based on current state
            task = HandleUserResponse(task);
        }
        
        // 2. Run the deliberation using Microsoft Agent Framework GroupChat
        var responseForUser = await RunDeliberationAsync(task, cancellationToken);
        
        // 3. Update state to awaiting user
        task.State = task.CurrentPhase switch
        {
            TaskPhase.BA => TaskState.AwaitingUser_BA,
            TaskPhase.Dev => TaskState.AwaitingUser_Dev,
            TaskPhase.QA => TaskState.AwaitingUser_QA,
            _ => task.State
        };
        
        // 4. Save the task state
        await _taskStore.UpdateTaskAsync(task);
        
        // 5. Stream the final response to the user with task ID appended
        var responseWithTaskId = TaskIdHelper.AppendTaskId(responseForUser, task.TaskId);
        
        await foreach (var chunk in StreamResponseAsync(responseWithTaskId, request.Model ?? "gpt-5-nano", cancellationToken))
        {
            yield return chunk;
        }
        
        // Store the assistant response in user messages
        task.UserMessages.Add(new ChatMessage
        {
            Role = "assistant",
            Content = responseWithTaskId
        });
        await _taskStore.UpdateTaskAsync(task);
    }

    private AgentTask HandleUserResponse(AgentTask task)
    {
        var lastUserMessage = task.UserMessages.LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase));
        var userContent = lastUserMessage?.Content?.ToLowerInvariant() ?? "";
        
        var isApproval = userContent.Contains("approve") || 
                         userContent.Contains("lgtm") || 
                         userContent.Contains("looks good") ||
                         userContent.Contains("proceed") ||
                         userContent.Contains("yes") ||
                         userContent.Contains("continue");
        
        if (isApproval && task.State.ToString().StartsWith("AwaitingUser"))
        {
            task = MoveToNextPhase(task);
        }
        else if (task.State.ToString().StartsWith("AwaitingUser"))
        {
            // User gave feedback, restart deliberation in current phase
            task.State = task.CurrentPhase switch
            {
                TaskPhase.BA => TaskState.BA_Working,
                TaskPhase.Dev => TaskState.Dev_Working,
                TaskPhase.QA => TaskState.QA_Working,
                _ => task.State
            };
            task.CurrentAgent = _agentRegistry.GetJuniorAgent(task.CurrentPhase);
            task.ResetDeliberationForNewPhase();
        }
        
        return task;
    }

    private AgentTask MoveToNextPhase(AgentTask task)
    {
        switch (task.CurrentPhase)
        {
            case TaskPhase.BA:
                task.CurrentPhase = TaskPhase.Dev;
                task.State = TaskState.Dev_Working;
                task.CurrentAgent = _agentRegistry.GetJuniorAgent(TaskPhase.Dev);
                task.ResetDeliberationForNewPhase();
                _logger.LogInformation("Task {TaskId} moving from BA to Dev phase", task.TaskId);
                break;
                
            case TaskPhase.Dev:
                task.CurrentPhase = TaskPhase.QA;
                task.State = TaskState.QA_Working;
                task.CurrentAgent = _agentRegistry.GetJuniorAgent(TaskPhase.QA);
                task.ResetDeliberationForNewPhase();
                _logger.LogInformation("Task {TaskId} moving from Dev to QA phase", task.TaskId);
                break;
                
            case TaskPhase.QA:
                task.State = TaskState.Completed;
                _logger.LogInformation("Task {TaskId} completed", task.TaskId);
                break;
        }
        
        return task;
    }

    /// <summary>
    /// Runs the deliberation using Microsoft Agent Framework GroupChat workflow.
    /// The framework handles the Jr ↔ Sr round-robin conversation automatically.
    /// </summary>
    private async Task<string> RunDeliberationAsync(AgentTask task, CancellationToken cancellationToken)
    {
        if (task.State == TaskState.Completed)
        {
            return "Task has been completed successfully! All phases (BA, Dev, QA) have been approved.";
        }
        
        _logger.LogInformation("Task {TaskId}: Running {Phase} phase deliberation", task.TaskId, task.CurrentPhase);
        
        // Build the GroupChat workflow for this phase
        var workflow = _deliberationWorkflow.BuildForPhase(task.CurrentPhase);
        
        // Convert user messages to Microsoft.Extensions.AI format
        var inputMessages = task.UserMessages
            .Select(m => new AIChatMessage(
                m.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? ChatRole.User : ChatRole.Assistant,
                m.Content ?? ""))
            .ToList();
        
        // Execute the workflow
        var run = await InProcessExecution.StreamAsync(workflow, inputMessages);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        
        // Collect all agent responses from the conversation
        var conversationBuilder = new StringBuilder();
        var agentResponses = new List<(string Agent, string Response)>();
        
        await foreach (var evt in run.WatchStreamAsync().WithCancellation(cancellationToken))
        {
            if (evt is AgentRunUpdateEvent update)
            {
                var response = update.AsResponse();
                foreach (var message in response.Messages)
                {
                    agentResponses.Add((update.ExecutorId, message.Text ?? ""));
                    _logger.LogDebug("Task {TaskId}: [{Agent}] {Response}", 
                        task.TaskId, update.ExecutorId, message.Text?[..Math.Min(100, message.Text?.Length ?? 0)]);
                }
            }
            else if (evt is WorkflowOutputEvent)
            {
                // Workflow completed
                break;
            }
        }
        
        // Format the final response for the user
        return FormatDeliberationOutput(task.CurrentPhase, agentResponses);
    }

    private static string FormatDeliberationOutput(TaskPhase phase, List<(string Agent, string Response)> responses)
    {
        var phaseLabel = phase switch
        {
            TaskPhase.BA => "Business Analysis",
            TaskPhase.Dev => "Development",
            TaskPhase.QA => "Quality Assurance",
            _ => phase.ToString()
        };
        
        var sb = new StringBuilder();
        sb.AppendLine($"## {phaseLabel} Phase");
        sb.AppendLine();
        
        foreach (var (agent, response) in responses)
        {
            sb.AppendLine($"### {agent}");
            sb.AppendLine(response);
            sb.AppendLine();
        }
        
        sb.AppendLine("---");
        sb.AppendLine("*Reply with **approve** to proceed to the next phase, or provide feedback for revisions.*");
        
        return sb.ToString();
    }

    private static async IAsyncEnumerable<ChatCompletionChunk> StreamResponseAsync(
        string content,
        string model,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var completionId = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        const int chunkSize = 10;
        for (var i = 0; i < content.Length; i += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var chunk = content.Substring(i, Math.Min(chunkSize, content.Length - i));
            
            yield return new ChatCompletionChunk
            {
                Id = completionId,
                Created = created,
                Model = model,
                Choices =
                [
                    new ChunkChoice
                    {
                        Index = 0,
                        Delta = new ChatMessageDelta
                        {
                            Content = chunk
                        },
                        FinishReason = null
                    }
                ]
            };
            
            await Task.Delay(5, cancellationToken);
        }
        
        yield return new ChatCompletionChunk
        {
            Id = completionId,
            Created = created,
            Model = model,
            Choices =
            [
                new ChunkChoice
                {
                    Index = 0,
                    Delta = new ChatMessageDelta(),
                    FinishReason = "stop"
                }
            ]
        };
    }
}
