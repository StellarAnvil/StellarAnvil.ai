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
            
            // Store tools from the request for agent function calling
            task.Tools = request.Tools;
            
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
            
            // Update tools if provided in continuation request
            if (request.Tools != null)
            {
                task.Tools = request.Tools;
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
        
        // 5. Stream the response content, then append task ID as final chunk
        await foreach (var chunk in StreamResponseWithTaskIdAsync(responseForUser, task.TaskId, request.Model ?? "gpt-5-nano", cancellationToken))
        {
            yield return chunk;
        }
        
        // Store the assistant response with task ID in user messages
        var responseWithTaskId = TaskIdHelper.AppendTaskId(responseForUser, task.TaskId);
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
        
        // Convert OpenAI tools to Microsoft.Extensions.AI AITools for agent use
        var aiTools = ToolConverter.ConvertToAITools(task.Tools);
        
        // Build the GroupChat workflow for this phase with tools support
        var workflow = _deliberationWorkflow.BuildForPhase(task.CurrentPhase, aiTools);
        
        // Convert user messages to Microsoft.Extensions.AI format
        var inputMessages = task.UserMessages
            .Select(m => new AIChatMessage(
                m.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? ChatRole.User : ChatRole.Assistant,
                m.Content ?? ""))
            .ToList();
        
        // Execute the workflow
        var run = await InProcessExecution.StreamAsync(workflow, inputMessages);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        
        // Collect agent responses - accumulate streaming tokens per agent
        var agentResponses = new List<(string Agent, string Response)>();
        string? currentAgent = null;
        var currentResponseBuilder = new StringBuilder();
        
        await foreach (var evt in run.WatchStreamAsync().WithCancellation(cancellationToken))
        {
            if (evt is AgentRunUpdateEvent update)
            {
                var response = update.AsResponse();
                
                // If agent changed, save previous agent's accumulated response
                if (currentAgent != null && currentAgent != update.ExecutorId)
                {
                    var accumulatedResponse = currentResponseBuilder.ToString().Trim();
                    if (!string.IsNullOrEmpty(accumulatedResponse))
                    {
                        agentResponses.Add((currentAgent, accumulatedResponse));
                        _logger.LogDebug("Task {TaskId}: [{Agent}] completed response", task.TaskId, currentAgent);
                    }
                    currentResponseBuilder.Clear();
                }
                
                currentAgent = update.ExecutorId;
                
                // Accumulate streaming tokens
                foreach (var message in response.Messages)
                {
                    currentResponseBuilder.Append(message.Text ?? "");
                }
            }
            else if (evt is WorkflowOutputEvent)
            {
                // Save final agent's response before workflow completes
                if (currentAgent != null)
                {
                    var accumulatedResponse = currentResponseBuilder.ToString().Trim();
                    if (!string.IsNullOrEmpty(accumulatedResponse))
                    {
                        agentResponses.Add((currentAgent, accumulatedResponse));
                    }
                }
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
            // Clean up agent name - remove hash suffix and format nicely
            var cleanAgentName = CleanAgentName(agent);
            sb.AppendLine($"### {cleanAgentName}");
            sb.AppendLine(response);
            sb.AppendLine();
        }
        
        sb.AppendLine("---");
        sb.AppendLine("*Reply with **approve** to proceed to the next phase, or provide feedback for revisions.*");
        
        return sb.ToString();
    }

    private static string CleanAgentName(string agentId)
    {
        // Agent IDs come as "business_analyst_f242e03183c849..." or "sr_business_analyst_..."
        // Extract just the meaningful part and format it nicely
        
        // Remove any hash suffix (32 char hex at end)
        var name = agentId;
        if (name.Length > 32)
        {
            var lastUnderscore = name.LastIndexOf('_');
            if (lastUnderscore > 0 && name.Length - lastUnderscore - 1 == 32)
            {
                name = name[..lastUnderscore];
            }
        }
        
        // Convert underscores to spaces and title case
        return string.Join(" ", name.Split('_')
            .Select(word => char.ToUpper(word[0]) + word[1..].ToLower()));
    }

    /// <summary>
    /// Streams content first, then sends task ID marker as the final chunk.
    /// This ensures task ID appears once at the end, not scattered throughout.
    /// </summary>
    private static async IAsyncEnumerable<ChatCompletionChunk> StreamResponseWithTaskIdAsync(
        string content,
        string taskId,
        string model,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var completionId = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        // Stream the main content
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
        
        // Send task ID marker as a single final content chunk (before finish)
        var taskIdMarker = $"\n\n<!-- task:{taskId} -->";
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
                        Content = taskIdMarker
                    },
                    FinishReason = null
                }
            ]
        };
        
        // Final chunk with finish reason
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
