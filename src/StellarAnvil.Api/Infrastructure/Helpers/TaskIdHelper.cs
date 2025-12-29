using System.Text.RegularExpressions;
using StellarAnvil.Api.Application.DTOs;

namespace StellarAnvil.Api.Infrastructure.Helpers;

public static partial class TaskIdHelper
{
    // Pattern: <!-- task:XXXXXXXX -->
    // Matches 8 alphanumeric characters (our task ID format)
    [GeneratedRegex(@"<!--\s*task:([a-zA-Z0-9]{8})\s*-->", RegexOptions.Compiled)]
    private static partial Regex TaskIdPattern();
    
    /// <summary>
    /// Extracts task ID from messages in the conversation history.
    /// Looks in both assistant and user messages (task ID may be embedded in user content
    /// when sent as continuation, or in assistant messages as response markers).
    /// Returns null if no task ID is found (indicating a fresh chat).
    /// </summary>
    public static string? ExtractTaskId(List<ChatMessage> messages)
    {
        // Scan all messages for task ID (from newest to oldest)
        // Check assistant messages first, then user messages
        foreach (var message in messages.AsEnumerable().Reverse())
        {
            if (string.IsNullOrEmpty(message.Content))
                continue;
            
            var match = TaskIdPattern().Match(message.Content);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Appends task ID marker to a response string.
    /// The marker is an HTML comment that is invisible when rendered as markdown.
    /// </summary>
    public static string AppendTaskId(string response, string taskId)
    {
        return $"{response}\n\n<!-- task:{taskId} -->";
    }
}


