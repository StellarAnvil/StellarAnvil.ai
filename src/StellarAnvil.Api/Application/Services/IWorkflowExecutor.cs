using StellarAnvil.Api.Application.Results;
using StellarAnvil.Api.Domain.Entities;

namespace StellarAnvil.Api.Application.Services;

/// <summary>
/// Executes the Manager-controlled workflow and collects agent responses.
/// </summary>
public interface IWorkflowExecutor
{
    /// <summary>
    /// Runs the deliberation workflow for a task.
    /// Returns early with tool calls if an agent requests function execution.
    /// </summary>
    Task<DeliberationResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken);
}

