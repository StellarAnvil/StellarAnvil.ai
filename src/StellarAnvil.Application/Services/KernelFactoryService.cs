using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using StellarAnvil.Application.Skills;

namespace StellarAnvil.Application.Services;

/// <summary>
/// Factory service for creating Semantic Kernel instances with different AI providers
/// </summary>
public interface IKernelFactoryService
{
    /// <summary>
    /// Create a kernel configured for the specified model
    /// </summary>
    Task<Kernel> CreateKernelForModelAsync(string model);
}

public class KernelFactoryService : IKernelFactoryService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public KernelFactoryService(IConfiguration configuration, IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    public Task<Kernel> CreateKernelForModelAsync(string model)
    {
        var builder = Kernel.CreateBuilder();
        
        // Configure the appropriate AI provider based on the model
        if (IsOpenAIModel(model))
        {
            var apiKey = _configuration["AI:OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException($"OpenAI API key not configured for model: {model}");
            }
            
#pragma warning disable SKEXP0010
            builder.AddOpenAIChatCompletion(model, apiKey);
#pragma warning restore SKEXP0010
        }
        else if (IsClaudeModel(model))
        {
            var apiKey = _configuration["AI:Claude:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException($"Claude API key not configured for model: {model}");
            }
            
            // TODO: Add Claude connector when available in future SK versions
            // For now, fallback to Ollama
            var ollamaBaseUrl = _configuration["AI:Ollama:BaseUrl"] ?? "http://localhost:11434";
            var ollamaModel = _configuration["AI:Ollama:DefaultModel"] ?? "Llama3.1:8B";
#pragma warning disable SKEXP0010
            builder.AddOpenAIChatCompletion(
                modelId: ollamaModel,
                apiKey: "not-needed",
                endpoint: new Uri($"{ollamaBaseUrl}/v1"));
#pragma warning restore SKEXP0010
        }
        else if (IsGeminiModel(model))
        {
            var apiKey = _configuration["AI:Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException($"Gemini API key not configured for model: {model}");
            }
            
            // TODO: Add Gemini connector when the correct package version is available
            // For now, fallback to Ollama
            var ollamaBaseUrl = _configuration["AI:Ollama:BaseUrl"] ?? "http://localhost:11434";
            var ollamaModel = _configuration["AI:Ollama:DefaultModel"] ?? "Llama3.1:8B";
#pragma warning disable SKEXP0010
            builder.AddOpenAIChatCompletion(
                modelId: ollamaModel,
                apiKey: "not-needed",
                endpoint: new Uri($"{ollamaBaseUrl}/v1"));
#pragma warning restore SKEXP0010
        }
        else
        {
            // Default to Ollama for any other model (including Llama3.1:8B)
            var ollamaBaseUrl = _configuration["AI:Ollama:BaseUrl"] ?? "http://localhost:11434";
#pragma warning disable SKEXP0010
            builder.AddOpenAIChatCompletion(
                modelId: model,
                apiKey: "not-needed",
                endpoint: new Uri($"{ollamaBaseUrl}/v1"));
#pragma warning restore SKEXP0010
        }

        // Register all skills with the kernel
        var continueDevSkills = _serviceProvider.GetRequiredService<ContinueDevSkills>();
        var jiraMcpSkills = _serviceProvider.GetRequiredService<JiraMcpSkills>();
        var taskManagementSkills = _serviceProvider.GetRequiredService<TaskManagementSkills>();
        
        builder.Plugins.AddFromObject(continueDevSkills, "ContinueDevSkills");
        builder.Plugins.AddFromObject(jiraMcpSkills, "JiraMcpSkills");
        builder.Plugins.AddFromObject(taskManagementSkills, "TaskManagementSkills");

        return Task.FromResult(builder.Build());
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
}
