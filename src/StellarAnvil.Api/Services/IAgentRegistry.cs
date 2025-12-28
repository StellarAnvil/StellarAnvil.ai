using StellarAnvil.Api.Models.Task;

namespace StellarAnvil.Api.Services;

public interface IAgentRegistry
{
    /// <summary>
    /// Gets the system prompt for a specific agent
    /// </summary>
    string GetSystemPrompt(string agentName);
    
    /// <summary>
    /// Gets the junior agent name for a phase (e.g., "business-analyst" for BA phase)
    /// </summary>
    string GetJuniorAgent(TaskPhase phase);
    
    /// <summary>
    /// Gets the senior agent name for a phase (e.g., "sr-business-analyst" for BA phase)
    /// </summary>
    string GetSeniorAgent(TaskPhase phase);
    
    /// <summary>
    /// Gets all available agent names
    /// </summary>
    IEnumerable<string> GetAllAgents();
}

