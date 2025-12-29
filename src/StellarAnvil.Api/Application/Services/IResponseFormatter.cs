namespace StellarAnvil.Api.Application.Services;

/// <summary>
/// Formats deliberation output for display to the user.
/// </summary>
public interface IResponseFormatter
{
    /// <summary>
    /// Formats agent responses into a readable markdown output.
    /// </summary>
    string FormatDeliberationOutput(List<(string Agent, string Response)> responses, bool isComplete);
}

