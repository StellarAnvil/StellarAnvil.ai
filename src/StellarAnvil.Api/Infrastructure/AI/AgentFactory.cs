using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using StellarAnvil.Api.Domain.Interfaces;

namespace StellarAnvil.Api.Infrastructure.AI;

public class AgentFactory : IAgentFactory
{
    private readonly IChatClient _chatClient;
    private readonly IChatClient _managerChatClient;
    private readonly IAgentRegistry _agentRegistry;

    public AgentFactory(IConfiguration configuration, IAgentRegistry agentRegistry)
    {
        _agentRegistry = agentRegistry;
        
        var apiKey = configuration["AI:OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("AI:OpenAI:ApiKey configuration is required");
        
        var model = configuration["AI:OpenAI:Model"] ?? "gpt-4o-mini";
        var managerModel = configuration["AI:OpenAI:ManagerModel"] ?? "gpt-4o-mini";
        
        var openAiClient = new OpenAIClient(apiKey);
        
        // Create OpenAI client and convert to IChatClient for Agent Framework
        _chatClient = openAiClient
            .GetChatClient(model)
            .AsIChatClient();
        
        // Create a lightweight chat client for Manager (fast model for quick decisions)
        _managerChatClient = openAiClient
            .GetChatClient(managerModel)
            .AsIChatClient();
    }

    public ChatClientAgent CreateAgent(string agentName)
    {
        return CreateAgent(agentName, tools: null);
    }
    
    public ChatClientAgent CreateAgent(string agentName, IList<AITool>? tools)
    {
        var systemPrompt = _agentRegistry.GetSystemPrompt(agentName);
        
        // If tools are provided, wrap the chat client to include them in every request
        var chatClient = tools != null && tools.Count > 0
            ? CreateChatClientWithTools(tools)
            : _chatClient;
        
        return new ChatClientAgent(
            chatClient,
            systemPrompt,
            agentName,
            $"{agentName} agent"
        );
    }
    
    /// <summary>
    /// Creates a wrapped IChatClient that includes the specified tools in every chat completion request.
    /// </summary>
    private IChatClient CreateChatClientWithTools(IList<AITool> tools)
    {
        // Use ConfigureOptions to add tools to every chat completion call
        return new ChatClientBuilder(_chatClient)
            .ConfigureOptions(options =>
            {
                options.Tools ??= [];
                foreach (var tool in tools)
                {
                    options.Tools.Add(tool);
                }
            })
            .Build();
    }
    
    /// <summary>
    /// Creates a lightweight IChatClient for the Manager agent.
    /// Used for quick speaker selection decisions in the workflow.
    /// </summary>
    public IChatClient CreateManagerChatClient()
    {
        return _managerChatClient;
    }
}
