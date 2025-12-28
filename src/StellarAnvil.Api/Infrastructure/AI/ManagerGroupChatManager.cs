using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace StellarAnvil.Api.Infrastructure.AI;

/// <summary>
/// Decision made by the Manager Agent for speaker selection.
/// </summary>
public record ManagerDecision(string NextAgent, string Reasoning);

/// <summary>
/// Custom GroupChatManager that uses an LLM (Manager Agent) to decide which agent should speak next.
/// This enables dynamic workflow routing based on conversation context and approval gates.
/// </summary>
public class ManagerGroupChatManager : GroupChatManager
{
    private readonly IReadOnlyList<ChatClientAgent> _agents;
    private readonly IChatClient _managerClient;
    private readonly string _managerPrompt;
    private readonly ILogger<ManagerGroupChatManager> _logger;
    
    /// <summary>
    /// Indicates that the workflow should pause and wait for user input.
    /// </summary>
    public bool IsAwaitingUser { get; private set; }
    
    /// <summary>
    /// Indicates that the workflow is complete.
    /// </summary>
    public bool IsComplete { get; private set; }
    
    /// <summary>
    /// The reasoning from the last Manager decision.
    /// </summary>
    public string? LastReasoning { get; private set; }

    public ManagerGroupChatManager(
        IEnumerable<ChatClientAgent> agents,
        IChatClient managerClient,
        string managerPrompt,
        ILogger<ManagerGroupChatManager> logger)
    {
        _agents = agents.ToList().AsReadOnly();
        _managerClient = managerClient;
        _managerPrompt = managerPrompt;
        _logger = logger;
        MaximumIterationCount = 20; // Allow enough iterations for multi-phase workflows
    }

    /// <summary>
    /// Selects the next speaker by asking the Manager Agent to analyze the conversation
    /// and decide which specialized agent should speak next.
    /// </summary>
#pragma warning disable CS8609 // Nullability of reference types in return type doesn't match overridden member - intentional for workflow control
    protected override async ValueTask<AIAgent?> SelectNextAgentAsync(
        IReadOnlyList<ChatMessage> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        IsAwaitingUser = false;
        IsComplete = false;
        
        try
        {
            // Build context for Manager to decide
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, _managerPrompt),
                new(ChatRole.User, FormatConversationForManager(conversationHistory))
            };
            
            // Ask Manager who should speak next
            var response = await _managerClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var responseText = response.Text ?? "";
            
            var decision = ParseManagerDecision(responseText);
            LastReasoning = decision.Reasoning;
            
            _logger.LogInformation(
                "Manager decided: {Agent} - {Reason}", 
                decision.NextAgent, 
                decision.Reasoning);
            
            // Handle special cases
            if (decision.NextAgent.Equals("AWAIT_USER", StringComparison.OrdinalIgnoreCase))
            {
                IsAwaitingUser = true;
                _logger.LogInformation("Manager signals: Awaiting user input");
                return null; // Signals end of this round, return to user
            }
            
            if (decision.NextAgent.Equals("COMPLETE", StringComparison.OrdinalIgnoreCase))
            {
                IsComplete = true;
                _logger.LogInformation("Manager signals: Workflow complete");
                return null; // Signals workflow completion
            }
            
            // Find and return the selected agent
            AIAgent? selectedAgent = _agents.FirstOrDefault(a => 
                a.Name?.Equals(decision.NextAgent, StringComparison.OrdinalIgnoreCase) == true);
            
            if (selectedAgent == null)
            {
                _logger.LogWarning(
                    "Manager selected unknown agent '{Agent}', falling back to first agent", 
                    decision.NextAgent);
                return _agents.FirstOrDefault();
            }
            
            return selectedAgent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Manager speaker selection, falling back to first agent");
            return _agents.FirstOrDefault();
        }
    }
#pragma warning restore CS8609

    /// <summary>
    /// Formats the conversation history for the Manager to analyze.
    /// </summary>
    private static string FormatConversationForManager(IReadOnlyList<ChatMessage> history)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CONVERSATION HISTORY:");
        sb.AppendLine("=====================");
        
        foreach (var msg in history)
        {
            var role = msg.Role.Value;
            var authorName = msg.AuthorName ?? role;
            var content = msg.Text ?? "(no content)";
            
            // Truncate very long messages for Manager context
            if (content.Length > 500)
            {
                content = content[..500] + "... (truncated)";
            }
            
            sb.AppendLine($"[{authorName}]: {content}");
        }
        
        sb.AppendLine();
        sb.AppendLine("Based on the conversation above, decide which agent should speak next.");
        sb.AppendLine("Respond with JSON only: {\"nextAgent\": \"...\", \"reasoning\": \"...\"}");
        
        return sb.ToString();
    }

    /// <summary>
    /// Parses the Manager's JSON response into a decision.
    /// </summary>
    private ManagerDecision ParseManagerDecision(string responseText)
    {
        try
        {
            // Try to extract JSON from the response (Manager might include extra text)
            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = responseText[jsonStart..(jsonEnd + 1)];
                var json = JsonDocument.Parse(jsonStr);
                
                var nextAgent = json.RootElement.GetProperty("nextAgent").GetString() ?? "business-analyst";
                var reasoning = json.RootElement.TryGetProperty("reasoning", out var reasonProp) 
                    ? reasonProp.GetString() ?? "No reasoning provided"
                    : "No reasoning provided";
                
                return new ManagerDecision(nextAgent, reasoning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Manager response as JSON: {Response}", responseText);
        }
        
        // Fallback: try to detect keywords in the response
        var lower = responseText.ToLowerInvariant();
        
        if (lower.Contains("await_user") || lower.Contains("await user"))
            return new ManagerDecision("AWAIT_USER", "Detected AWAIT_USER in response");
        
        if (lower.Contains("complete"))
            return new ManagerDecision("COMPLETE", "Detected COMPLETE in response");
        
        if (lower.Contains("developer") && !lower.Contains("sr-developer") && !lower.Contains("sr_developer"))
            return new ManagerDecision("developer", "Detected developer in response");
        
        if (lower.Contains("sr-developer") || lower.Contains("sr_developer") || lower.Contains("senior developer"))
            return new ManagerDecision("sr-developer", "Detected sr-developer in response");
        
        if (lower.Contains("business-analyst") || lower.Contains("business analyst"))
            return new ManagerDecision("business-analyst", "Detected business-analyst in response");
        
        if (lower.Contains("sr-business-analyst") || lower.Contains("senior business analyst"))
            return new ManagerDecision("sr-business-analyst", "Detected sr-business-analyst in response");
        
        if (lower.Contains("quality-assurance") || lower.Contains("quality assurance") || lower.Contains("qa"))
            return new ManagerDecision("quality-assurance", "Detected quality-assurance in response");
        
        if (lower.Contains("sr-quality-assurance") || lower.Contains("senior qa"))
            return new ManagerDecision("sr-quality-assurance", "Detected sr-quality-assurance in response");
        
        // Ultimate fallback
        _logger.LogWarning("Could not parse Manager response, defaulting to business-analyst: {Response}", responseText);
        return new ManagerDecision("business-analyst", "Fallback to default agent");
    }
}

