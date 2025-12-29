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
}
