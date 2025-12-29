using System.Text.Json;
using Microsoft.Extensions.AI;
using StellarAnvil.Api.Application.Results;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace StellarAnvil.Api.Application.Mappers;

/// <summary>
/// Extracts tool/function calls from agent response messages.
/// </summary>
public static class ToolCallExtractor
{
    /// <summary>
    /// Extracts tool/function calls from Microsoft.Extensions.AI response messages.
    /// Returns Application-layer models, not transport DTOs.
    /// </summary>
    public static List<RequestedToolCall> ExtractToolCalls(IList<AIChatMessage> messages)
    {
        var toolCalls = new List<RequestedToolCall>();
        
        foreach (var message in messages)
        {
            foreach (var content in message.Contents)
            {
                if (content is FunctionCallContent functionCall)
                {
                    // Convert arguments dictionary to JSON string
                    var argumentsJson = functionCall.Arguments != null 
                        ? JsonSerializer.Serialize(functionCall.Arguments)
                        : "{}";
                    
                    toolCalls.Add(new RequestedToolCall(
                        CallId: functionCall.CallId ?? $"call_{Guid.NewGuid():N}",
                        FunctionName: functionCall.Name ?? "unknown",
                        Arguments: argumentsJson
                    ));
                }
            }
        }
        
        return toolCalls;
    }
}
