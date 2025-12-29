using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using StellarAnvil.Api.Application.Results;
using StellarAnvil.Api.Application.UseCases;
using StellarAnvil.Api.Domain.Interfaces;

namespace StellarAnvil.Api.Infrastructure.AI;

public class DeliberationWorkflow : IDeliberationWorkflow
{
    private readonly IAgentFactory _agentFactory;
    private readonly IAgentRegistry _agentRegistry;
    private readonly ILogger<DeliberationWorkflow> _logger;
    private readonly ILoggerFactory _loggerFactory;
    
    private static readonly string[] AllAgentNames = 
    {
        "business-analyst", "sr-business-analyst",
        "developer", "sr-developer", 
        "quality-assurance", "sr-quality-assurance"
    };

    public DeliberationWorkflow(
        IAgentFactory agentFactory, 
        IAgentRegistry agentRegistry,
        ILogger<DeliberationWorkflow> logger,
        ILoggerFactory loggerFactory)
    {
        _agentFactory = agentFactory;
        _agentRegistry = agentRegistry;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Builds a Manager-controlled GroupChat with all 6 phase agents.
    /// The Manager decides speaker order dynamically based on conversation.
    /// </summary>
    public WorkflowBuildResult Build(IList<AITool>? tools = null)
    {
        _logger.LogInformation(
            "Building Manager-controlled workflow with {AgentCount} agents and {ToolCount} tools",
            AllAgentNames.Length, tools?.Count ?? 0);
        
        var agents = AllAgentNames
            .Select(name => _agentFactory.CreateAgent(name, tools))
            .ToArray();
        
        var managerClient = _agentFactory.CreateManagerChatClient();
        var managerPrompt = _agentRegistry.GetSystemPrompt("manager");
        var managerLogger = _loggerFactory.CreateLogger<ManagerGroupChatManager>();
        
        var managerInstance = new ManagerGroupChatManager(
            agents,
            managerClient, 
            managerPrompt,
            managerLogger);
        
        var workflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(_ => managerInstance)
            .AddParticipants(agents)
            .Build();
        
        return new WorkflowBuildResult(workflow, managerInstance);
    }
    
    /// <summary>
    /// Gets an IChatClient for direct agent calls (bypassing the workflow).
    /// Used for tool result continuations where we want to continue with a specific agent.
    /// </summary>
    public IChatClient? GetAgentChatClient(string agentName, IList<AITool>? tools = null)
    {
        // Extract the base agent name from the full ID (e.g., "developer_abc123" -> "developer")
        var baseName = ExtractBaseAgentName(agentName);
        
        if (!AllAgentNames.Contains(baseName))
        {
            _logger.LogWarning("Unknown agent name: {AgentName}, base: {BaseName}", agentName, baseName);
            return null;
        }
        
        _logger.LogInformation("Creating direct chat client for agent: {AgentName}", baseName);
        return _agentFactory.CreateChatClientWithTools(tools);
    }
    
    /// <summary>
    /// Gets the system prompt for a specific agent.
    /// </summary>
    public string GetAgentSystemPrompt(string agentName)
    {
        var baseName = ExtractBaseAgentName(agentName);
        return _agentRegistry.GetSystemPrompt(baseName);
    }
    
    /// <summary>
    /// Extracts the base agent name from a full agent ID.
    /// Agent IDs come as "developer_abc123..." - we need just "developer".
    /// </summary>
    private static string ExtractBaseAgentName(string agentId)
    {
        // Agent names in AllAgentNames use hyphens: "business-analyst", "sr-developer", etc.
        // Agent IDs from the workflow use underscores: "developer_abc123", "sr_developer_abc123"
        
        // First, check if it's already a base name
        if (AllAgentNames.Contains(agentId))
        {
            return agentId;
        }
        
        // Try to match against known patterns
        foreach (var name in AllAgentNames)
        {
            // Convert hyphen to underscore for comparison
            var underscoreName = name.Replace('-', '_');
            if (agentId.StartsWith(underscoreName + "_") || agentId.StartsWith(name + "_"))
            {
                return name;
            }
        }
        
        // Fallback: try to extract by removing the hash suffix
        var underscoreIndex = agentId.LastIndexOf('_');
        if (underscoreIndex > 0)
        {
            var potentialName = agentId[..underscoreIndex].Replace('_', '-');
            if (AllAgentNames.Contains(potentialName))
            {
                return potentialName;
            }
        }
        
        // Last resort: return as-is
        return agentId;
    }
}


