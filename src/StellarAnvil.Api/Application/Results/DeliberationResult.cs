namespace StellarAnvil.Api.Application.Results;

/// <summary>
/// Result of running a Manager-controlled deliberation workflow.
/// </summary>
public sealed record DeliberationResult(string Response, bool IsComplete);

