namespace StellarAnvil.Api.Application.Results;

/// <summary>
/// Represents a tool call requested by an agent during workflow execution.
/// This is an Application-layer model, separate from transport DTOs.
/// </summary>
public sealed record RequestedToolCall(
    string CallId,
    string FunctionName,
    string Arguments
);

