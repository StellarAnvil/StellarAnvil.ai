using Microsoft.Agents.AI.Workflows;
using StellarAnvil.Api.Models.Task;

namespace StellarAnvil.Api.Services;

public interface IDeliberationWorkflow
{
    /// <summary>
    /// Builds a GroupChat workflow for the specified phase (BA, Dev, QA)
    /// with Junior and Senior agents in round-robin conversation
    /// </summary>
    Workflow BuildForPhase(TaskPhase phase);
}

public class DeliberationWorkflow : IDeliberationWorkflow
{
    private readonly IAgentFactory _agentFactory;
    private readonly IAgentRegistry _agentRegistry;
    private readonly ILogger<DeliberationWorkflow> _logger;
    
    private const int MaxIterations = 5; // Jr -> Sr -> Jr -> Sr -> Jr (5 turns)

    public DeliberationWorkflow(
        IAgentFactory agentFactory, 
        IAgentRegistry agentRegistry,
        ILogger<DeliberationWorkflow> logger)
    {
        _agentFactory = agentFactory;
        _agentRegistry = agentRegistry;
        _logger = logger;
    }

    public Workflow BuildForPhase(TaskPhase phase)
    {
        var juniorAgentName = _agentRegistry.GetJuniorAgent(phase);
        var seniorAgentName = _agentRegistry.GetSeniorAgent(phase);
        
        _logger.LogInformation(
            "Building deliberation workflow for {Phase} phase: {Junior} <-> {Senior}",
            phase, juniorAgentName, seniorAgentName);
        
        var juniorAgent = _agentFactory.CreateAgent(juniorAgentName);
        var seniorAgent = _agentFactory.CreateAgent(seniorAgentName);
        
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

