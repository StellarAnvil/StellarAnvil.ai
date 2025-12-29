using System.Text.Json;
using Microsoft.Extensions.AI;
using StellarAnvil.Api.Application.DTOs;

namespace StellarAnvil.Api.Application.Helpers;

/// <summary>
/// Converts OpenAI-format tools to Microsoft.Extensions.AI AITool format
/// </summary>
public static class ToolConverter
{
    /// <summary>
    /// Converts a list of OpenAI Tool definitions to AITool instances
    /// that can be passed to IChatClient via ChatOptions.Tools
    /// </summary>
    public static IList<AITool>? ConvertToAITools(List<Tool>? tools)
    {
        if (tools == null || tools.Count == 0)
        {
            return null;
        }

        var aiTools = new List<AITool>();
        
        foreach (var tool in tools)
        {
            if (tool.Type == "function" && tool.Function != null)
            {
                var aiFunction = CreateAIFunction(tool.Function);
                aiTools.Add(aiFunction);
            }
        }

        return aiTools.Count > 0 ? aiTools : null;
    }

    private static AITool CreateAIFunction(FunctionDefinition function)
    {
        // Convert parameters to JsonElement for the schema
        var parametersSchema = function.Parameters != null
            ? JsonSerializer.SerializeToElement(function.Parameters)
            : default;

        // Use CreateDeclaration for tool definitions without implementation
        // The actual execution happens client-side (Cursor/VS Code calls the tool)
        return AIFunctionFactory.CreateDeclaration(
            function.Name,
            function.Description,
            parametersSchema);
    }
}

