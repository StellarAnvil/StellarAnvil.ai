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
        services.AddScoped<IKernelFactoryService, KernelFactoryService>();

        // Skills
        services.AddScoped<ContinueDevSkills>();
        services.AddScoped<JiraMcpSkills>();
        services.AddScoped<UxDesignSkills>();
        services.AddScoped<TaskManagementSkills>();
        services.AddHttpClient<JiraMcpSkills>();
        services.AddHttpClient<McpConfigurationService>();

        return services;
    }
}

