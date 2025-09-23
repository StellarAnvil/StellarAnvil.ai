using StellarAnvil.Domain.Entities;
using StellarAnvil.Domain.Enums;

namespace StellarAnvil.Infrastructure.Data;

public static class DataSeeder
{
    public static async System.Threading.Tasks.Task SeedDefaultDataAsync(StellarAnvilDbContext context)
    {
        if (!context.Workflows.Any())
        {
            await SeedDefaultWorkflowsAsync(context);
        }

        if (!context.ApiKeys.Any())
        {
            await SeedDefaultApiKeysAsync(context);
        }

        await context.SaveChangesAsync();
    }

    private static System.Threading.Tasks.Task SeedDefaultWorkflowsAsync(StellarAnvilDbContext context)
    {
        var workflows = new List<Workflow>
        {
            // Simple SDLC: PO → BA → Dev → QA → Done
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Simple SDLC",
                Description = "Simple workflow for basic tasks: Planning → Requirements → Development → QA → Done",
                IsDefault = true,
                Transitions = new List<WorkflowTransition>
                {
                    new() { FromState = WorkflowState.Planning, ToState = WorkflowState.RequirementsAnalysis, RequiredRole = TeamMemberRole.ProductOwner, Order = 1 },
                    new() { FromState = WorkflowState.RequirementsAnalysis, ToState = WorkflowState.Development, RequiredRole = TeamMemberRole.BusinessAnalyst, Order = 2 },
                    new() { FromState = WorkflowState.Development, ToState = WorkflowState.QualityAssurance, RequiredRole = TeamMemberRole.Developer, Order = 3 },
                    new() { FromState = WorkflowState.QualityAssurance, ToState = WorkflowState.Completed, RequiredRole = TeamMemberRole.QualityAssurance, Order = 4 }
                }
            },

            // Standard SDLC: PO → BA → Architect → Dev → QA → Done
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Standard SDLC",
                Description = "Standard workflow with architecture: Planning → Requirements → Architecture → Development → QA → Done",
                IsDefault = true,
                Transitions = new List<WorkflowTransition>
                {
                    new() { FromState = WorkflowState.Planning, ToState = WorkflowState.RequirementsAnalysis, RequiredRole = TeamMemberRole.ProductOwner, Order = 1 },
                    new() { FromState = WorkflowState.RequirementsAnalysis, ToState = WorkflowState.ArchitecturalDesign, RequiredRole = TeamMemberRole.BusinessAnalyst, Order = 2 },
                    new() { FromState = WorkflowState.ArchitecturalDesign, ToState = WorkflowState.Development, RequiredRole = TeamMemberRole.Architect, Order = 3 },
                    new() { FromState = WorkflowState.Development, ToState = WorkflowState.QualityAssurance, RequiredRole = TeamMemberRole.Developer, Order = 4 },
                    new() { FromState = WorkflowState.QualityAssurance, ToState = WorkflowState.Completed, RequiredRole = TeamMemberRole.QualityAssurance, Order = 5 }
                }
            },

            // Full SDLC: PO → BA → Architect → UX → Dev → QA → Done
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Full SDLC",
                Description = "Full workflow with UX: Planning → Requirements → Architecture → UX Design → Development → QA → Done",
                IsDefault = true,
                Transitions = new List<WorkflowTransition>
                {
                    new() { FromState = WorkflowState.Planning, ToState = WorkflowState.RequirementsAnalysis, RequiredRole = TeamMemberRole.ProductOwner, Order = 1 },
                    new() { FromState = WorkflowState.RequirementsAnalysis, ToState = WorkflowState.ArchitecturalDesign, RequiredRole = TeamMemberRole.BusinessAnalyst, Order = 2 },
                    new() { FromState = WorkflowState.ArchitecturalDesign, ToState = WorkflowState.UXDesign, RequiredRole = TeamMemberRole.Architect, Order = 3 },
                    new() { FromState = WorkflowState.UXDesign, ToState = WorkflowState.Development, RequiredRole = TeamMemberRole.UXDesigner, Order = 4 },
                    new() { FromState = WorkflowState.Development, ToState = WorkflowState.QualityAssurance, RequiredRole = TeamMemberRole.Developer, Order = 5 },
                    new() { FromState = WorkflowState.QualityAssurance, ToState = WorkflowState.Completed, RequiredRole = TeamMemberRole.QualityAssurance, Order = 6 }
                }
            }
        };

        context.Workflows.AddRange(workflows);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    private static System.Threading.Tasks.Task SeedDefaultApiKeysAsync(StellarAnvilDbContext context)
    {
        var apiKeys = new List<ApiKey>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Key = "admin-key-" + Guid.NewGuid().ToString("N")[..16],
                Type = ApiKeyType.Admin,
                Name = "Default Admin Key",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Key = "openapi-key-" + Guid.NewGuid().ToString("N")[..16],
                Type = ApiKeyType.OpenApi,
                Name = "Default OpenAPI Key",
                IsActive = true
            }
        };

        context.ApiKeys.AddRange(apiKeys);
        return System.Threading.Tasks.Task.CompletedTask;
    }
}