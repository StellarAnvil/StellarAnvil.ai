namespace StellarAnvil.Api.Domain.Interfaces;

public interface IAgentRegistry
{
    /// <summary>
    /// Gets the system prompt for a specific agent
    /// </summary>
    string GetSystemPrompt(string agentName);
}


