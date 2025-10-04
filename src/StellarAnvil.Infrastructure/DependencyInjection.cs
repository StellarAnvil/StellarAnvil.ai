using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StellarAnvil.Domain.Services;
using StellarAnvil.Infrastructure.Data;
using StellarAnvil.Infrastructure.Repositories;
using StellarAnvil.Infrastructure.Services;
using StellarAnvil.Infrastructure.AI;

namespace StellarAnvil.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<StellarAnvilDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Domain Services
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<ITeamMemberService, TeamMemberService>();
        services.AddScoped<IAutoGenGeminiService, AutoGenGeminiService>();


            return services;
    }
}
