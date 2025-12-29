namespace StellarAnvil.Api.Application.Formatters;

/// <summary>
/// Formats agent names for display purposes.
/// </summary>
public static class AgentNameFormatter
{
    /// <summary>
    /// Cleans and formats an agent ID for display.
    /// Agent IDs come as "business_analyst_f242e03183c849..." or "sr_business_analyst_..."
    /// This extracts just the meaningful part and formats it nicely.
    /// </summary>
    public static string CleanAgentName(string agentId)
    {
        // Remove any hash suffix (32 char hex at end)
        var name = agentId;
        if (name.Length > 32)
        {
            var lastUnderscore = name.LastIndexOf('_');
            if (lastUnderscore > 0 && name.Length - lastUnderscore - 1 == 32)
            {
                name = name[..lastUnderscore];
            }
        }
        
        // Convert underscores/hyphens to spaces and title case
        return string.Join(" ", name.Replace('-', '_').Split('_')
            .Where(word => word.Length > 0)
            .Select(word => char.ToUpper(word[0]) + (word.Length > 1 ? word[1..].ToLower() : "")));
    }
}

