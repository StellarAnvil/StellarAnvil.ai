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
}
