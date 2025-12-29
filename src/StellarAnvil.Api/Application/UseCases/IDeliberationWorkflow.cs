using Microsoft.Extensions.AI;
using StellarAnvil.Api.Application.Results;

namespace StellarAnvil.Api.Application.UseCases;

public interface IDeliberationWorkflow
{
    /// <summary>
    /// Builds a Manager-controlled GroupChat workflow with all agents.
    /// The Manager Agent decides which agent speaks next based on conversation context.
    /// </summary>
    WorkflowBuildResult Build(IList<AITool>? tools = null);
    
    /// <summary>
    /// Gets an IChatClient for a specific agent by name (for direct continuation after tool calls).
    /// The agent name should match the pattern: developer_xxx, sr-developer_xxx, etc.
    /// </summary>
    IChatClient? GetAgentChatClient(string agentName, IList<AITool>? tools = null);
    
    /// <summary>
    /// Gets the system prompt for a specific agent.
    /// </summary>
    string GetAgentSystemPrompt(string agentName);
}


