using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using StellarAnvil.Application.Services;
using StellarAnvil.Application.Mappings;
using StellarAnvil.Application.Skills;
using StellarAnvil.Domain.Services;

namespace StellarAnvil.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        // AutoMapper
        services.AddAutoMapper(typeof(MappingProfile));

        // Application Services
        services.AddScoped<ITeamMemberApplicationService, TeamMemberApplicationService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ITaskApplicationService, TaskApplicationService>();
        services.AddScoped<IMcpConfigurationService, McpConfigurationService>();
        services.AddScoped<AutoGenCollaborationService>();
        services.AddScoped<WorkflowStateMachine>();
        services.AddScoped<WorkflowPlannerService>();
        services.AddScoped<ISystemPromptService, SystemPromptService>();
        services.AddScoped<IAIClientService, AIClientService>();

        // Skills
        services.AddScoped<ContinueDevSkills>();
        services.AddScoped<JiraMcpSkills>();
        services.AddScoped<UxDesignSkills>();
        services.AddScoped<TaskManagementSkills>();
        services.AddHttpClient<JiraMcpSkills>();
        services.AddHttpClient<McpConfigurationService>();


        // Semantic Kernel
        services.AddSingleton<Kernel>(provider =>
        {
            var builder = Kernel.CreateBuilder();
            
            // Check for OpenAI API key first (from user secrets, environment variables, or appsettings)
            var openAiApiKey = configuration["AI:OpenAI:ApiKey"];
            if (false)
            {
                // Use OpenAI if API key is provided (supports function calling)
                var openAiModel = configuration["AI:OpenAI:DefaultModel"] ?? "gpt-5-mini";
                builder.AddOpenAIChatCompletion(openAiModel, openAiApiKey);
            }
            else
            {
                // Fallback to Ollama if no OpenAI API key
                var ollamaBaseUrl = configuration["AI:Ollama:BaseUrl"] ?? "http://localhost:11434";
#pragma warning disable SKEXP0010
                builder.AddOpenAIChatCompletion(
                    modelId: "Llama3.1:8B",
                    apiKey: "not-needed", // Ollama doesn't require API key
                    endpoint: new Uri($"{ollamaBaseUrl}/v1"));
#pragma warning restore SKEXP0010
            }

            // Add Claude support if API key is provided
            var claudeApiKey = configuration["AI:Claude:ApiKey"];
            if (!string.IsNullOrEmpty(claudeApiKey))
            {
                // Add Claude connector when available in future SK versions
                // builder.AddAnthropicChatCompletion("claude-3-sonnet-20240229", claudeApiKey);
            }

            // Add Gemini support if API key is provided
            var geminiApiKey = configuration["AI:Gemini:ApiKey"];
            if (!string.IsNullOrEmpty(geminiApiKey))
            {
                // TODO: Add Gemini connector when the correct package version is available
                // var geminiModel = configuration["AI:Gemini:DefaultModel"] ?? "gemini-pro";
                // builder.AddGoogleAIChatCompletion(geminiModel, geminiApiKey);
            }

            // Register skills
            var continueDevSkills = provider.GetRequiredService<ContinueDevSkills>();
            var jiraMcpSkills = provider.GetRequiredService<JiraMcpSkills>();
            var taskManagementSkills = provider.GetRequiredService<TaskManagementSkills>();
            
            builder.Plugins.AddFromObject(continueDevSkills, "ContinueDevSkills");
            builder.Plugins.AddFromObject(jiraMcpSkills, "JiraMcpSkills");
            builder.Plugins.AddFromObject(taskManagementSkills, "TaskManagementSkills");

            return builder.Build();
        });

        return services;
    }
}

