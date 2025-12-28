using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace StellarAnvil.Api.Services;

public interface IAgentFactory
{
    /// <summary>
    /// Creates a ChatClientAgent for the specified agent name using its system prompt
    /// </summary>
    ChatClientAgent CreateAgent(string agentName);
}

public class AgentFactory : IAgentFactory
{
    private readonly IChatClient _chatClient;
    private readonly IAgentRegistry _agentRegistry;

    public AgentFactory(IConfiguration configuration, IAgentRegistry agentRegistry)
    {
        _agentRegistry = agentRegistry;
        
        var apiKey = configuration["AI:OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("AI:OpenAI:ApiKey configuration is required");
        
        var model = configuration["AI:OpenAI:Model"] ?? "gpt-4o-mini";
        
        // Create OpenAI client and convert to IChatClient for Agent Framework
        _chatClient = new OpenAIClient(apiKey)
            .GetChatClient(model)
            .AsIChatClient();
    }

    public ChatClientAgent CreateAgent(string agentName)
    {
        var systemPrompt = _agentRegistry.GetSystemPrompt(agentName);
        
        return new ChatClientAgent(
            _chatClient,
            systemPrompt,
            agentName,
            $"{agentName} agent"
        );
    }
}

