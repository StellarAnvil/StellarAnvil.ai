using StellarAnvil.Api.Models.Task;

namespace StellarAnvil.Api.Services;

public class AgentRegistry : IAgentRegistry
{
    private readonly Dictionary<string, string> _prompts = new();
    private readonly ILogger<AgentRegistry> _logger;
    
    // Phase to agent mapping
    private static readonly Dictionary<TaskPhase, (string Junior, string Senior)> PhaseAgents = new()
    {
        [TaskPhase.BA] = ("business-analyst", "sr-business-analyst"),
        [TaskPhase.Dev] = ("developer", "sr-developer"),
        [TaskPhase.QA] = ("quality-assurance", "sr-quality-assurance")
    };
    
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
    
    public string GetJuniorAgent(TaskPhase phase)
    {
        return PhaseAgents[phase].Junior;
    }
    
    public string GetSeniorAgent(TaskPhase phase)
    {
        return PhaseAgents[phase].Senior;
    }
    
    public IEnumerable<string> GetAllAgents()
    {
        return _prompts.Keys;
    }
}

