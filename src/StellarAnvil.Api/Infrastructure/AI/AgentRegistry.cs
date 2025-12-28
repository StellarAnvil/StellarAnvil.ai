using StellarAnvil.Api.Domain.Interfaces;

namespace StellarAnvil.Api.Infrastructure.AI;

public class AgentRegistry : IAgentRegistry
{
    private readonly Dictionary<string, string> _prompts = new();
    private readonly ILogger<AgentRegistry> _logger;
    
    public AgentRegistry(IWebHostEnvironment environment, ILogger<AgentRegistry> logger)
    {
        _logger = logger;
        LoadPrompts(environment);
    }
    
    private void LoadPrompts(IWebHostEnvironment environment)
    {
        // SystemPrompts folder is at the root of the solution, not in the API project
        var promptsPath = Path.Combine(environment.ContentRootPath, "..", "..", "SystemPrompts");
        
        if (!Directory.Exists(promptsPath))
        {
            // Fallback: check if it's directly in content root (for published scenarios)
            promptsPath = Path.Combine(environment.ContentRootPath, "SystemPrompts");
        }
        
        if (!Directory.Exists(promptsPath))
        {
            _logger.LogWarning("SystemPrompts directory not found at {Path}", promptsPath);
            return;
        }
        
        foreach (var file in Directory.GetFiles(promptsPath, "*.txt"))
        {
            var agentName = Path.GetFileNameWithoutExtension(file);
            var content = File.ReadAllText(file);
            _prompts[agentName] = content;
            _logger.LogInformation("Loaded prompt for agent: {AgentName}", agentName);
        }
        
        _logger.LogInformation("Loaded {Count} agent prompts", _prompts.Count);
    }
    
    public string GetSystemPrompt(string agentName)
    {
        if (_prompts.TryGetValue(agentName, out var prompt))
        {
            return prompt;
        }
        
        _logger.LogWarning("Prompt not found for agent: {AgentName}, using default", agentName);
        return _prompts.GetValueOrDefault("default", "You are a helpful assistant.");
    }
}

