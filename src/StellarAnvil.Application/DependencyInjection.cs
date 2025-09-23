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
        services.AddHttpClient<JiraMcpSkills>();
        services.AddHttpClient<McpConfigurationService>();


        // Semantic Kernel
        services.AddSingleton<Kernel>(provider =>
        {
            var builder = Kernel.CreateBuilder();
            
            var openAiApiKey = configuration["AI:OpenAI:ApiKey"];
            if (!string.IsNullOrEmpty(openAiApiKey))
            {
                builder.AddOpenAIChatCompletion("gpt-4", openAiApiKey);
            }

            var claudeApiKey = configuration["AI:Claude:ApiKey"];
            if (!string.IsNullOrEmpty(claudeApiKey))
            {
                // Add Claude connector when available
            }

            var geminiApiKey = configuration["AI:Gemini:ApiKey"];
            if (!string.IsNullOrEmpty(geminiApiKey))
            {
                // Add Gemini connector when available
            }

            // Register skills
            var continueDevSkills = provider.GetRequiredService<ContinueDevSkills>();
            var jiraMcpSkills = provider.GetRequiredService<JiraMcpSkills>();
            
            builder.Plugins.AddFromObject(continueDevSkills, "ContinueDevSkills");
            builder.Plugins.AddFromObject(jiraMcpSkills, "JiraMcpSkills");

            return builder.Build();
        });

        return services;
    }
}

