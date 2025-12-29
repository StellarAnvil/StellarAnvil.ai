using System.Text;
using Microsoft.Agents.AI.Workflows;
using StellarAnvil.Api.Application.Helpers;
using StellarAnvil.Api.Application.Mappers;
using StellarAnvil.Api.Application.Results;
using StellarAnvil.Api.Application.UseCases;
using StellarAnvil.Api.Domain.Entities;

namespace StellarAnvil.Api.Application.Services;

/// <summary>
/// Executes the Manager-controlled workflow and collects agent responses.
/// </summary>
public class WorkflowExecutor : IWorkflowExecutor
{
    private readonly IDeliberationWorkflow _deliberationWorkflow;
    private readonly IResponseFormatter _responseFormatter;
    private readonly ILogger<WorkflowExecutor> _logger;

    public WorkflowExecutor(
        IDeliberationWorkflow deliberationWorkflow,
        IResponseFormatter responseFormatter,
        ILogger<WorkflowExecutor> logger)
    {
        _deliberationWorkflow = deliberationWorkflow;
        _responseFormatter = responseFormatter;
        _logger = logger;
    }

    public async Task<DeliberationResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken)
    {
        if (task.State == TaskState.Completed)
        {
            return DeliberationResult.TextResponse(
                "Task has been completed successfully! All work has been approved.", true);
        }
        
        _logger.LogInformation("Task {TaskId}: Running Manager-controlled deliberation", task.TaskId);
        
        // Convert OpenAI tools to Microsoft.Extensions.AI AITools for agent use
        var aiTools = ToolConverter.ConvertToAITools(task.Tools);
        
        // Build the Manager-controlled GroupChat workflow with all agents
        var workflowResult = _deliberationWorkflow.Build(aiTools);
        
        // Convert user messages to Microsoft.Extensions.AI format (including tool results)
        var inputMessages = AiMessageMapper.ConvertToAIMessages(task.UserMessages);
        
        LogInputMessages(task.TaskId, inputMessages);
        
        // Execute the workflow
        var run = await InProcessExecution.StreamAsync(workflowResult.Workflow, inputMessages);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        
        // Collect responses from the workflow stream
        var agentResponses = new List<(string Agent, string Response)>();
        string? currentAgent = null;
        var currentResponseBuilder = new StringBuilder();
        DeliberationResult? toolCallResult = null;
        
        try
        {
            await foreach (var evt in run.WatchStreamAsync().WithCancellation(cancellationToken))
            {
                _logger.LogDebug("Task {TaskId}: Received workflow event: {EventType}", 
                    task.TaskId, evt.GetType().Name);
                
                if (evt is AgentRunUpdateEvent update)
                {
                    var response = update.AsResponse();
                    _logger.LogInformation("Task {TaskId}: AgentRunUpdate from [{Agent}], Messages={Count}", 
                        task.TaskId, update.ExecutorId, response.Messages.Count);
                    
                    // Check for tool calls - if found, stop and return
                    var toolCalls = ToolCallExtractor.ExtractToolCalls(response.Messages);
                    if (toolCalls.Count > 0)
                    {
                        _logger.LogInformation("Task {TaskId}: Agent {Agent} requested {Count} tool calls",
                            task.TaskId, update.ExecutorId, toolCalls.Count);
                        
                        task.LastActiveAgent = update.ExecutorId;
                        toolCallResult = DeliberationResult.ToolCallResponse(toolCalls, update.ExecutorId);
                        
                        await run.CancelRunAsync();
                        break;
                    }
                    
                    // Handle agent change - save accumulated response
                    if (currentAgent != null && currentAgent != update.ExecutorId)
                    {
                        SaveAccumulatedResponse(agentResponses, currentAgent, currentResponseBuilder, task.TaskId);
                        currentResponseBuilder.Clear();
                    }
                    
                    currentAgent = update.ExecutorId;
                    
                    // Accumulate streaming tokens
                    AccumulateResponseTokens(response.Messages, currentResponseBuilder, task.TaskId, update.ExecutorId);
                }
                else if (evt is WorkflowOutputEvent)
                {
                    // Save final agent's response
                    if (currentAgent != null)
                    {
                        SaveAccumulatedResponse(agentResponses, currentAgent, currentResponseBuilder, task.TaskId);
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
        
        LogCapturedResponses(task.TaskId, agentResponses);
        
        _logger.LogInformation(
            "Task {TaskId}: Deliberation complete. IsComplete={IsComplete}, IsAwaitingUser={IsAwaitingUser}, Reason={Reason}",
            task.TaskId, workflowResult.Manager.IsComplete, workflowResult.Manager.IsAwaitingUser, 
            workflowResult.Manager.LastReasoning);
        
        // Format the final response for the user
        var formattedResponse = _responseFormatter.FormatDeliberationOutput(agentResponses, isComplete);
        
        return DeliberationResult.TextResponse(formattedResponse, isComplete);
    }

    private void SaveAccumulatedResponse(
        List<(string Agent, string Response)> responses,
        string agent,
        StringBuilder responseBuilder,
        string taskId)
    {
        var accumulatedResponse = responseBuilder.ToString().Trim();
        if (!string.IsNullOrEmpty(accumulatedResponse))
        {
            responses.Add((agent, accumulatedResponse));
            _logger.LogDebug("Task {TaskId}: [{Agent}] completed response", taskId, agent);
        }
    }

    private void AccumulateResponseTokens(
        IList<Microsoft.Extensions.AI.ChatMessage> messages,
        StringBuilder responseBuilder,
        string taskId,
        string agentId)
    {
        foreach (var message in messages)
        {
            var text = message.Text ?? "";
            if (!string.IsNullOrEmpty(text))
            {
                _logger.LogDebug("Task {TaskId}: [{Agent}] text chunk: {Text}", 
                    taskId, agentId, text.Length > 100 ? text[..100] + "..." : text);
            }
            responseBuilder.Append(text);
        }
    }

    private void LogInputMessages(string taskId, List<Microsoft.Extensions.AI.ChatMessage> messages)
    {
        _logger.LogInformation("Task {TaskId}: Sending {Count} messages to workflow:", taskId, messages.Count);
        foreach (var msg in messages)
        {
            var contentPreview = msg.Text?.Length > 200 ? msg.Text[..200] + "..." : msg.Text;
            var contentType = msg.Contents.FirstOrDefault()?.GetType().Name ?? "none";
            _logger.LogInformation("  [{Role}] ({ContentType}) {Preview}", msg.Role, contentType, contentPreview);
        }
    }

    private void LogCapturedResponses(string taskId, List<(string Agent, string Response)> responses)
    {
        _logger.LogInformation("Task {TaskId}: Captured {Count} agent responses:", taskId, responses.Count);
        foreach (var (agent, response) in responses)
        {
            var preview = response.Length > 100 ? response[..100] + "..." : response;
            _logger.LogInformation("  [{Agent}]: {Preview}", agent, preview);
        }
    }
}
