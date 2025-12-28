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
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        IDeliberationWorkflow deliberationWorkflow,
        ITaskStore taskStore,
        ILogger<AgentOrchestrator> logger)
    {
        _deliberationWorkflow = deliberationWorkflow;
        _taskStore = taskStore;
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
            
            // Manager will decide the starting phase dynamically
            task.State = TaskState.Working;
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
            
            // Manager will handle user response (approval/feedback) - no manual handling needed
            task.State = TaskState.Working;
        }
        
        // 2. Run the Manager-controlled deliberation
        var (responseForUser, isComplete) = await RunManagerDeliberationAsync(task, cancellationToken);
        
        // 3. Update state based on Manager's decision
        task.State = isComplete ? TaskState.Completed : TaskState.AwaitingUser;
        
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

    /// <summary>
    /// Runs the Manager-controlled GroupChat workflow.
    /// The Manager Agent decides which agents speak and handles phase transitions.
    /// Returns the formatted response and whether the workflow is complete.
    /// </summary>
    private async Task<(string Response, bool IsComplete)> RunManagerDeliberationAsync(
        AgentTask task, 
        CancellationToken cancellationToken)
    {
        if (task.State == TaskState.Completed)
        {
            return ("Task has been completed successfully! All work has been approved.", true);
        }
        
        _logger.LogInformation("Task {TaskId}: Running Manager-controlled deliberation", task.TaskId);
        
        // Convert OpenAI tools to Microsoft.Extensions.AI AITools for agent use
        var aiTools = ToolConverter.ConvertToAITools(task.Tools);
        
        // Build the Manager-controlled GroupChat workflow with all agents
        var (workflow, manager) = _deliberationWorkflow.Build(aiTools);
        
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
        
        // Check if Manager signals completion
        var isComplete = manager.IsComplete;
        
        _logger.LogInformation(
            "Task {TaskId}: Deliberation complete. IsComplete={IsComplete}, IsAwaitingUser={IsAwaitingUser}, Reason={Reason}",
            task.TaskId, manager.IsComplete, manager.IsAwaitingUser, manager.LastReasoning);
        
        // Format the final response for the user
        var formattedResponse = FormatDeliberationOutput(agentResponses, isComplete);
        
        return (formattedResponse, isComplete);
    }

    private static string FormatDeliberationOutput(List<(string Agent, string Response)> responses, bool isComplete)
    {
        var sb = new StringBuilder();
        
        foreach (var (agent, response) in responses)
        {
            // Clean up agent name - remove hash suffix and format nicely
            var cleanAgentName = CleanAgentName(agent);
            sb.AppendLine($"### {cleanAgentName}");
            sb.AppendLine(response);
            sb.AppendLine();
        }
        
        sb.AppendLine("---");
        
        if (isComplete)
        {
            sb.AppendLine("**Task Complete!** All work has been reviewed and approved.");
        }
        else
        {
            sb.AppendLine("*Reply with **approve** to proceed to the next phase, or provide feedback for revisions.*");
        }
        
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
        
        // Convert underscores/hyphens to spaces and title case
        return string.Join(" ", name.Replace('-', '_').Split('_')
            .Where(word => word.Length > 0)
            .Select(word => char.ToUpper(word[0]) + (word.Length > 1 ? word[1..].ToLower() : "")));
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
