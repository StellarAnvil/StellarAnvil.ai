using Microsoft.Agents.AI.Workflows;
using StellarAnvil.Api.Infrastructure.AI;

namespace StellarAnvil.Api.Application.Results;

/// <summary>
/// Result of building a Manager-controlled GroupChat workflow.
/// </summary>
public sealed record WorkflowBuildResult(Workflow Workflow, ManagerGroupChatManager Manager);

