using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace StellarAnvil.Api.Infrastructure.AI;

public interface IAgentFactory
{
    /// <summary>
    /// Creates a ChatClientAgent for the specified agent name using its system prompt
    /// </summary>
    ChatClientAgent CreateAgent(string agentName);
    
    /// <summary>
    /// Creates a ChatClientAgent for the specified agent name with tools support
    /// </summary>
    ChatClientAgent CreateAgent(string agentName, IList<AITool>? tools);
    
    /// <summary>
    /// Creates a lightweight IChatClient for the Manager agent (workflow orchestration).
    /// Uses a fast model for quick speaker selection decisions.
    /// </summary>
    IChatClient CreateManagerChatClient();
}
