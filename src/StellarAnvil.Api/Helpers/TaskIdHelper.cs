using System.Text.RegularExpressions;
using StellarAnvil.Api.Models.OpenAI;

namespace StellarAnvil.Api.Helpers;

public static partial class TaskIdHelper
{
    // Pattern: <!-- task:XXXXXXXX -->
    // Matches 8 alphanumeric characters (our task ID format)
    [GeneratedRegex(@"<!--\s*task:([a-zA-Z0-9]{8})\s*-->", RegexOptions.Compiled)]
    private static partial Regex TaskIdPattern();
    
    /// <summary>
    /// Extracts task ID from assistant messages in the conversation history.
    /// Returns null if no task ID is found (indicating a fresh chat).
    /// </summary>
    public static string? ExtractTaskId(List<ChatMessage> messages)
    {
        // Scan assistant messages for task ID (from newest to oldest)
        foreach (var message in messages.AsEnumerable().Reverse())
        {
            if (!message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                continue;
            
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
    
    /// <summary>
    /// Removes task ID marker from a message content.
    /// Useful for displaying clean content without the marker.
    /// </summary>
    public static string RemoveTaskId(string content)
    {
        return TaskIdPattern().Replace(content, "").Trim();
    }
}

