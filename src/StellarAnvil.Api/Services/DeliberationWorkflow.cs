using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using StellarAnvil.Api.Models.Task;

namespace StellarAnvil.Api.Services;

public interface IDeliberationWorkflow
{
    /// <summary>
    /// Builds a Manager-controlled GroupChat workflow with all agents.
    /// The Manager Agent decides which agent speaks next based on conversation context.
    /// </summary>
    (Workflow Workflow, ManagerGroupChatManager Manager) Build(IList<AITool>? tools = null);
    
    /// <summary>
    /// [Legacy] Builds a GroupChat workflow for the specified phase (BA, Dev, QA)
    /// with Junior and Senior agents in round-robin conversation
    /// </summary>
    Workflow BuildForPhase(TaskPhase phase);
    
    /// <summary>
    /// [Legacy] Builds a GroupChat workflow for the specified phase with tools support
    /// </summary>
    Workflow BuildForPhase(TaskPhase phase, IList<AITool>? tools);
}

public class DeliberationWorkflow : IDeliberationWorkflow
{
    private readonly IAgentFactory _agentFactory;
    private readonly IAgentRegistry _agentRegistry;
    private readonly ILogger<DeliberationWorkflow> _logger;
    private readonly ILoggerFactory _loggerFactory;
    
    // All agents in the Manager-controlled workflow
    private static readonly string[] AllAgentNames = 
    {
        "business-analyst", "sr-business-analyst",
        "developer", "sr-developer", 
        "quality-assurance", "sr-quality-assurance"
    };
    
    private const int MaxIterations = 5; // Jr -> Sr -> Jr -> Sr -> Jr (5 turns) - for legacy

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
    public (Workflow Workflow, ManagerGroupChatManager Manager) Build(IList<AITool>? tools = null)
    {
        _logger.LogInformation(
            "Building Manager-controlled workflow with {AgentCount} agents and {ToolCount} tools",
            AllAgentNames.Length, tools?.Count ?? 0);
        
        // Create all 6 phase agents
        var agents = AllAgentNames
            .Select(name => _agentFactory.CreateAgent(name, tools))
            .ToArray();
        
        // Create Manager's chat client (lightweight, fast model)
        var managerClient = _agentFactory.CreateManagerChatClient();
        var managerPrompt = _agentRegistry.GetSystemPrompt("manager");
        var managerLogger = _loggerFactory.CreateLogger<ManagerGroupChatManager>();
        
        // Create the Manager BEFORE the builder - pass it the agents we're going to use
        var managerInstance = new ManagerGroupChatManager(
            agents,
            managerClient, 
            managerPrompt,
            managerLogger);
        
        // Build GroupChat with Manager controlling speaker selection
        var workflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(_ => managerInstance)  // Return the pre-created manager
            .AddParticipants(agents)
            .Build();
        
        return (workflow, managerInstance);
    }

    // ============ Legacy methods for backwards compatibility ============
    
    public Workflow BuildForPhase(TaskPhase phase)
    {
        return BuildForPhase(phase, tools: null);
    }
    
    public Workflow BuildForPhase(TaskPhase phase, IList<AITool>? tools)
    {
        var juniorAgentName = _agentRegistry.GetJuniorAgent(phase);
        var seniorAgentName = _agentRegistry.GetSeniorAgent(phase);
        
        _logger.LogInformation(
            "[Legacy] Building deliberation workflow for {Phase} phase: {Junior} <-> {Senior} with {ToolCount} tools",
            phase, juniorAgentName, seniorAgentName, tools?.Count ?? 0);
        
        var juniorAgent = _agentFactory.CreateAgent(juniorAgentName, tools);
        var seniorAgent = _agentFactory.CreateAgent(seniorAgentName, tools);
        
        // Build group chat with round-robin speaker selection
        // Junior speaks first, then Senior reviews, alternating up to MaxIterations
        var workflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(agents => 
                new RoundRobinGroupChatManager(agents) 
                { 
                    MaximumIterationCount = MaxIterations
                })
            .AddParticipants(juniorAgent, seniorAgent)
            .Build();
        
        return workflow;
    }
}

