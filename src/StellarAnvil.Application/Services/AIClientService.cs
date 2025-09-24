using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StellarAnvil.Domain.Services;
using StellarAnvil.Infrastructure.AI;
using System.Text.Json;
using System.Text;

namespace StellarAnvil.Application.Services;

public class AIClientService : IAIClientService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AIClientService> _logger;

    public AIClientService(
        IConfiguration configuration, 
        IHttpClientFactory httpClientFactory,
        ILogger<AIClientService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IChatClient> GetClientForModelAsync(string? model)
    {
        // Default to deepseek-r1 if no model specified
        model = model ?? "deepseek-r1";
        
        _logger.LogInformation("Getting client for model: {Model}", model);

        // Determine provider based on model name
        if (IsOpenAIModel(model))
        {
            var apiKey = _configuration["AI:OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException($"OpenAI API key not configured for model: {model}");
            }
            return CreateOpenAIClient(apiKey, model);
        }
        
        if (IsClaudeModel(model))
        {
            var apiKey = _configuration["AI:Claude:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException($"Claude API key not configured for model: {model}");
            }
            return CreateClaudeClient(apiKey, model);
        }
        
        if (IsGeminiModel(model))
        {
            var apiKey = _configuration["AI:Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException($"Gemini API key not configured for model: {model}");
            }
            return CreateGeminiClient(apiKey, model);
        }
        
        // Default to Ollama for any other model (including deepseek-r1)
        return CreateOllamaClient(model);
    }

    public async Task<List<string>> GetSupportedModelsAsync()
    {
        var models = new List<string>();
        
        // Add OpenAI models if API key is configured
        if (!string.IsNullOrEmpty(_configuration["AI:OpenAI:ApiKey"]))
        {
            models.AddRange(new[] { "gpt-4o", "gpt-4o-mini", "gpt-4", "gpt-3.5-turbo" });
        }
        
        // Add Claude models if API key is configured
        if (!string.IsNullOrEmpty(_configuration["AI:Claude:ApiKey"]))
        {
            models.AddRange(new[] { "claude-3-opus-20240229", "claude-3-sonnet-20240229", "claude-3-haiku-20240307" });
        }
        
        // Add Gemini models if API key is configured
        if (!string.IsNullOrEmpty(_configuration["AI:Gemini:ApiKey"]))
        {
            models.AddRange(new[] { "gemini-pro", "gemini-pro-vision" });
        }
        
        // Always add Ollama models (no API key needed)
        models.AddRange(new[] { "deepseek-r1", "llama3", "codellama" });
        
        return models;
    }

    private static bool IsOpenAIModel(string model)
    {
        return model.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase);
    }
    
    private static bool IsClaudeModel(string model)
    {
        return model.StartsWith("claude-", StringComparison.OrdinalIgnoreCase);
    }
    
    private static bool IsGeminiModel(string model)
    {
        return model.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase);
    }

    private IChatClient CreateOpenAIClient(string apiKey, string model)
    {
        // TODO: Implement OpenAI client
        // For now, throw not implemented
        throw new NotImplementedException("OpenAI client not yet implemented");
    }
    
    private IChatClient CreateClaudeClient(string apiKey, string model)
    {
        // TODO: Implement Claude client  
        // For now, throw not implemented
        throw new NotImplementedException("Claude client not yet implemented");
    }
    
    private IChatClient CreateGeminiClient(string apiKey, string model)
    {
        // TODO: Implement Gemini client
        // For now, throw not implemented  
        throw new NotImplementedException("Gemini client not yet implemented");
    }
    
    private IChatClient CreateOllamaClient(string model)
    {
        var baseUrl = _configuration["AI:Ollama:BaseUrl"] ?? "http://localhost:11434";
        var httpClient = _httpClientFactory.CreateClient();
        return new OllamaChatClient(httpClient, baseUrl, model);
    }
}
