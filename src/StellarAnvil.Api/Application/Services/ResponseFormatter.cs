using System.Text;
using StellarAnvil.Api.Application.Formatters;

namespace StellarAnvil.Api.Application.Services;

/// <summary>
/// Formats deliberation output for display to the user.
/// </summary>
public class ResponseFormatter : IResponseFormatter
{
    public string FormatDeliberationOutput(List<(string Agent, string Response)> responses, bool isComplete)
    {
        var sb = new StringBuilder();
        
        foreach (var (agent, response) in responses)
        {
            var cleanAgentName = AgentNameFormatter.CleanAgentName(agent);
            sb.AppendLine($"### {cleanAgentName}");
            sb.AppendLine(response);
            sb.AppendLine();
        }
        
        sb.AppendLine("---");
        
        if (isComplete)
        {
            sb.AppendLine("**Task Complete!** All work has been reviewed and approved.");
        }
        else
        {
            sb.AppendLine("*Reply with **approve** to proceed to the next phase, or provide feedback for revisions.*");
        }
        
        return sb.ToString();
    }
}

