using System.Text.Json;
using Microsoft.Extensions.AI;
using StellarAnvil.Api.Application.DTOs;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatMessage = StellarAnvil.Api.Application.DTOs.ChatMessage;

namespace StellarAnvil.Api.Application.Mappers;

/// <summary>
/// Maps OpenAI-format messages to Microsoft.Extensions.AI format.
/// </summary>
public static class AiMessageMapper
{
    /// <summary>
    /// Converts OpenAI-format messages to Microsoft.Extensions.AI format,
    /// including proper handling of tool results.
    /// </summary>
    public static List<AIChatMessage> ConvertToAIMessages(List<ChatMessage> messages)
    {
        var aiMessages = new List<AIChatMessage>();
        
        foreach (var m in messages)
        {
            if (m.Role.Equals("tool", StringComparison.OrdinalIgnoreCase))
            {
                // Tool result message - convert to FunctionResultContent
                var resultContent = new FunctionResultContent(
                    callId: m.ToolCallId ?? "",
                    result: m.Content);
                
                var toolMessage = new AIChatMessage(ChatRole.Tool, [resultContent]);
                aiMessages.Add(toolMessage);
            }
            else if (m.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) && m.ToolCalls?.Count > 0)
            {
                // Assistant message with tool calls - convert to FunctionCallContent
                var contents = new List<AIContent>();
                
                // Add any text content first
                if (!string.IsNullOrEmpty(m.Content))
                {
                    contents.Add(new TextContent(m.Content));
                }
                
                // Add tool calls
                foreach (var tc in m.ToolCalls)
                {
                    var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(tc.Function.Arguments) 
                        ?? new Dictionary<string, object?>();
                    contents.Add(new FunctionCallContent(tc.Id, tc.Function.Name, args));
                }
                
                var assistantMessage = new AIChatMessage(ChatRole.Assistant, contents);
                aiMessages.Add(assistantMessage);
            }
            else
            {
                // Regular user/assistant message
                var role = m.Role.Equals("user", StringComparison.OrdinalIgnoreCase) 
                    ? ChatRole.User 
                    : ChatRole.Assistant;
                aiMessages.Add(new AIChatMessage(role, m.Content ?? ""));
            }
        }
        
        return aiMessages;
    }
}

