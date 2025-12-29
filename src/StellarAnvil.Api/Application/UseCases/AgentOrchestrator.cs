using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Agents.AI.Workflows;
using StellarAnvil.Api.Application.DTOs;
using StellarAnvil.Api.Application.Formatters;
using StellarAnvil.Api.Application.Helpers;
using StellarAnvil.Api.Application.Mappers;
using StellarAnvil.Api.Application.Results;
using StellarAnvil.Api.Application.Streaming;
using StellarAnvil.Api.Domain.Entities;
using StellarAnvil.Api.Domain.Interfaces;
using ChatMessage = StellarAnvil.Api.Application.DTOs.ChatMessage;

namespace StellarAnvil.Api.Application.UseCases;

public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IDeliberationWorkflow _deliberationWorkflow;
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        IDeliberationWorkflow deliberationWorkflow,
        ITaskRepository taskRepository,
        ILogger<AgentOrchestrator> logger)
    {
        _deliberationWorkflow = deliberationWorkflow;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async IAsyncEnumerable<ChatCompletionChunk> ProcessAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1. Detect if this is fresh or continuation
        var taskId = TaskIdHelper.ExtractTaskId(request.Messages);
        AgentTask task;
        
        // Check if this request contains tool results (role="tool" messages)
        var toolResultMessages = request.Messages
            .Where(m => m.Role.Equals("tool", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var hasToolResults = toolResultMessages.Count > 0;
        
        if (taskId == null)
        {
            // Fresh chat - create new task
            task = await _taskRepository.CreateTaskAsync();
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
            task = await _taskRepository.GetTaskAsync(taskId) 
                ?? throw new InvalidOperationException($"Task {taskId} not found");
            _logger.LogInformation("Resuming task {TaskId} in state {State}, HasToolResults={HasToolResults}", 
                task.TaskId, task.State, hasToolResults);
            
            // IMPORTANT: Cursor sends the FULL conversation history in each request.
            // Use request.Messages as the authoritative source to avoid duplicate accumulation.
            // Filter out system messages and use the rest as our conversation history.
            var conversationMessages = request.Messages
                .Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            task.UserMessages = conversationMessages;
            _logger.LogInformation("Task {TaskId}: Using {Count} messages from request as conversation history", 
                task.TaskId, conversationMessages.Count);
            
            if (hasToolResults && task.State == TaskState.AwaitingToolResult)
            {
                // Log tool results for debugging
                foreach (var toolResult in toolResultMessages)
                {
                    _logger.LogInformation("Task {TaskId}: Has tool result for {ToolCallId}", 
                        task.TaskId, toolResult.ToolCallId);
                }
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
        }
        
        // 2. Run deliberation - always use fresh workflow
        // Note: Checkpointing doesn't work because agent IDs are generated dynamically with GUIDs.
        // Instead, we pass the full conversation history (including tool calls + results) and let
        // the Manager route correctly based on context.
        task.State = TaskState.Working;
        var deliberationResult = await RunManagerDeliberationAsync(task, cancellationToken);
        
        // 3. Handle result based on type (tool calls vs text response)
        if (deliberationResult.HasToolCalls)
        {
            // Tool calls detected - emit them and stop immediately
            _logger.LogInformation("Task {TaskId}: Emitting {Count} tool calls, stopping stream", 
                task.TaskId, deliberationResult.ToolCalls.Count);
            
            // Store pending tool calls for validation on next request
            task.PendingToolCalls = deliberationResult.ToolCalls
                .Select(tc => new PendingToolCall(tc.CallId, tc.FunctionName, tc.Arguments))
                .ToList();
            task.LastActiveAgent = deliberationResult.ToolCallAgent;
            task.State = TaskState.AwaitingToolResult;
            
            // Store the assistant message with tool calls (convert to DTOs for storage)
            task.UserMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = null,
                ToolCalls = OpenAiStreamWriter.ToToolCallDtos(deliberationResult.ToolCalls)
            });
            
            await _taskRepository.UpdateTaskAsync(task);
            
            // Stream tool calls to client and stop immediately
            await foreach (var chunk in OpenAiStreamWriter.StreamToolCallsAsync(
                deliberationResult.ToolCalls, task.TaskId, request.Model ?? "gpt-5-nano", cancellationToken))
            {
                yield return chunk;
            }
        }
        else
        {
            // Normal text response
            task.State = deliberationResult.IsComplete ? TaskState.Completed : TaskState.AwaitingUser;
            await _taskRepository.UpdateTaskAsync(task);
            
            // Stream the response content, then append task ID as final chunk
            await foreach (var chunk in OpenAiStreamWriter.StreamResponseWithTaskIdAsync(
                deliberationResult.Response, task.TaskId, request.Model ?? "gpt-5-nano", cancellationToken))
            {
                yield return chunk;
            }
            
            // Store the assistant response with task ID in user messages
            var responseWithTaskId = TaskIdHelper.AppendTaskId(deliberationResult.Response, task.TaskId);
            task.UserMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = responseWithTaskId
            });
            await _taskRepository.UpdateTaskAsync(task);
        }
    }

    /// <summary>
    /// Runs the Manager-controlled GroupChat workflow.
    /// The Manager Agent decides which agents speak and handles phase transitions.
    /// Returns early with tool calls if an agent requests function execution.
    /// </summary>
    private async Task<DeliberationResult> RunManagerDeliberationAsync(
        AgentTask task, 
        CancellationToken cancellationToken)
    {
        if (task.State == TaskState.Completed)
        {
            return DeliberationResult.TextResponse("Task has been completed successfully! All work has been approved.", true);
        }
        
        _logger.LogInformation("Task {TaskId}: Running Manager-controlled deliberation", task.TaskId);
        
        // Convert OpenAI tools to Microsoft.Extensions.AI AITools for agent use
        var aiTools = ToolConverter.ConvertToAITools(task.Tools);
        
        // Build the Manager-controlled GroupChat workflow with all agents
        var workflowResult = _deliberationWorkflow.Build(aiTools);
        
        // Convert user messages to Microsoft.Extensions.AI format (including tool results)
        var inputMessages = AiMessageMapper.ConvertToAIMessages(task.UserMessages);
        
        // DEBUG: Log the messages being sent to the workflow
        _logger.LogInformation("Task {TaskId}: Sending {Count} messages to workflow:", task.TaskId, inputMessages.Count);
        foreach (var msg in inputMessages)
        {
            var contentPreview = msg.Text?.Length > 200 ? msg.Text[..200] + "..." : msg.Text;
            var contentType = msg.Contents.FirstOrDefault()?.GetType().Name ?? "none";
            _logger.LogInformation("  [{Role}] ({ContentType}) {Preview}", msg.Role, contentType, contentPreview);
        }
        
        // Execute the workflow (no checkpointing - agent IDs are dynamic)
        var run = await InProcessExecution.StreamAsync(workflowResult.Workflow, inputMessages);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        
        // Collect agent responses - accumulate streaming tokens per agent
        var agentResponses = new List<(string Agent, string Response)>();
        string? currentAgent = null;
        var currentResponseBuilder = new StringBuilder();
        DeliberationResult? toolCallResult = null;
        
        try
        {
            await foreach (var evt in run.WatchStreamAsync().WithCancellation(cancellationToken))
            {
                _logger.LogDebug("Task {TaskId}: Received workflow event: {EventType}", task.TaskId, evt.GetType().Name);
                
                if (evt is AgentRunUpdateEvent update)
                {
                    var response = update.AsResponse();
                    _logger.LogInformation("Task {TaskId}: AgentRunUpdate from [{Agent}], Messages={Count}", 
                        task.TaskId, update.ExecutorId, response.Messages.Count);
                    
                    // Check for tool/function calls in the response - if found, stop and return to client
                    var toolCalls = ToolCallExtractor.ExtractToolCalls(response.Messages);
                    if (toolCalls.Count > 0)
                    {
                        _logger.LogInformation("Task {TaskId}: Agent {Agent} requested {Count} tool calls",
                            task.TaskId, update.ExecutorId, toolCalls.Count);
                        
                        // Store the agent that made the tool call for context
                        task.LastActiveAgent = update.ExecutorId;
                        
                        // Store tool call result
                        toolCallResult = DeliberationResult.ToolCallResponse(toolCalls, update.ExecutorId);
                        
                        // Cancel the workflow to stop processing
                        await run.CancelRunAsync();
                        break;
                    }
                
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
                    
                    // Accumulate streaming tokens (text content only)
                    foreach (var message in response.Messages)
                    {
                        var text = message.Text ?? "";
                        if (!string.IsNullOrEmpty(text))
                        {
                            _logger.LogDebug("Task {TaskId}: [{Agent}] text chunk: {Text}", 
                                task.TaskId, update.ExecutorId, text.Length > 100 ? text[..100] + "..." : text);
                        }
                        currentResponseBuilder.Append(text);
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
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Task {TaskId}: Workflow stream cancelled", task.TaskId);
        }
        finally
        {
            await run.DisposeAsync();
        }
        
        // If we detected tool calls, return them
        if (toolCallResult != null)
        {
            _logger.LogInformation("Task {TaskId}: Returning tool calls", task.TaskId);
            return toolCallResult;
        }
        
        // Check if Manager signals completion
        var isComplete = workflowResult.Manager.IsComplete;
        
        // DEBUG: Log what we captured
        _logger.LogInformation("Task {TaskId}: Captured {Count} agent responses:", task.TaskId, agentResponses.Count);
        foreach (var (agent, response) in agentResponses)
        {
            var preview = response.Length > 100 ? response[..100] + "..." : response;
            _logger.LogInformation("  [{Agent}]: {Preview}", agent, preview);
        }
        
        _logger.LogInformation(
            "Task {TaskId}: Deliberation complete. IsComplete={IsComplete}, IsAwaitingUser={IsAwaitingUser}, Reason={Reason}",
            task.TaskId, workflowResult.Manager.IsComplete, workflowResult.Manager.IsAwaitingUser, workflowResult.Manager.LastReasoning);
        
        // Format the final response for the user
        var formattedResponse = FormatDeliberationOutput(agentResponses, isComplete);
        
        return DeliberationResult.TextResponse(formattedResponse, isComplete);
    }

    private static string FormatDeliberationOutput(List<(string Agent, string Response)> responses, bool isComplete)
    {
        var sb = new StringBuilder();
        
        foreach (var (agent, response) in responses)
        {
            // Clean up agent name - remove hash suffix and format nicely
            var cleanAgentName = AgentNameFormatter.CleanAgentName(agent);
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
}
