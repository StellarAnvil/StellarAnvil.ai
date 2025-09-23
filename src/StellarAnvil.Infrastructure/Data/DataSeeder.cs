using Microsoft.EntityFrameworkCore;
using StellarAnvil.Domain.Entities;
using StellarAnvil.Domain.Enums;

namespace StellarAnvil.Infrastructure.Data;

public static class DataSeeder
{
    public static async System.Threading.Tasks.Task SeedDefaultDataAsync(StellarAnvilDbContext context)
    {
        // Check if data already exists
        if (await context.Workflows.AnyAsync())
        {
            return; // Data already seeded
        }

        // Seed default workflows
        var fullWorkflowId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var standardWorkflowId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var simpleWorkflowId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        // Full workflow: PO -> BA -> Architect -> UX -> Dev -> QA -> Done
        context.Workflows.Add(new Workflow
        {
            Id = fullWorkflowId,
            Name = "Full SDLC Workflow",
            Description = "Complete software development lifecycle with all phases",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        context.WorkflowTransitions.AddRange(
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = fullWorkflowId, FromState = WorkflowState.Planning, ToState = WorkflowState.RequirementsAnalysis, RequiredRole = TeamMemberRole.ProductOwner, Order = 1, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = fullWorkflowId, FromState = WorkflowState.RequirementsAnalysis, ToState = WorkflowState.ArchitecturalDesign, RequiredRole = TeamMemberRole.BusinessAnalyst, Order = 2, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = fullWorkflowId, FromState = WorkflowState.ArchitecturalDesign, ToState = WorkflowState.UXDesign, RequiredRole = TeamMemberRole.Architect, Order = 3, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = fullWorkflowId, FromState = WorkflowState.UXDesign, ToState = WorkflowState.Development, RequiredRole = TeamMemberRole.UXDesigner, Order = 4, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = fullWorkflowId, FromState = WorkflowState.Development, ToState = WorkflowState.QualityAssurance, RequiredRole = TeamMemberRole.Developer, Order = 5, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = fullWorkflowId, FromState = WorkflowState.QualityAssurance, ToState = WorkflowState.Completed, RequiredRole = TeamMemberRole.QualityAssurance, Order = 6, CreatedAt = DateTime.UtcNow }
        );

        // Standard workflow: PO -> BA -> Architect -> Dev -> QA -> Done
        context.Workflows.Add(new Workflow
        {
            Id = standardWorkflowId,
            Name = "Standard SDLC Workflow",
            Description = "Standard software development lifecycle without UX design",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        context.WorkflowTransitions.AddRange(
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = standardWorkflowId, FromState = WorkflowState.Planning, ToState = WorkflowState.RequirementsAnalysis, RequiredRole = TeamMemberRole.ProductOwner, Order = 1, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = standardWorkflowId, FromState = WorkflowState.RequirementsAnalysis, ToState = WorkflowState.ArchitecturalDesign, RequiredRole = TeamMemberRole.BusinessAnalyst, Order = 2, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = standardWorkflowId, FromState = WorkflowState.ArchitecturalDesign, ToState = WorkflowState.Development, RequiredRole = TeamMemberRole.Architect, Order = 3, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = standardWorkflowId, FromState = WorkflowState.Development, ToState = WorkflowState.QualityAssurance, RequiredRole = TeamMemberRole.Developer, Order = 4, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = standardWorkflowId, FromState = WorkflowState.QualityAssurance, ToState = WorkflowState.Completed, RequiredRole = TeamMemberRole.QualityAssurance, Order = 5, CreatedAt = DateTime.UtcNow }
        );

        // Simple workflow: PO -> BA -> Dev -> QA -> Done
        context.Workflows.Add(new Workflow
        {
            Id = simpleWorkflowId,
            Name = "Simple SDLC Workflow",
            Description = "Simplified software development lifecycle for small changes",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        context.WorkflowTransitions.AddRange(
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = simpleWorkflowId, FromState = WorkflowState.Planning, ToState = WorkflowState.RequirementsAnalysis, RequiredRole = TeamMemberRole.ProductOwner, Order = 1, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = simpleWorkflowId, FromState = WorkflowState.RequirementsAnalysis, ToState = WorkflowState.Development, RequiredRole = TeamMemberRole.BusinessAnalyst, Order = 2, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = simpleWorkflowId, FromState = WorkflowState.Development, ToState = WorkflowState.QualityAssurance, RequiredRole = TeamMemberRole.Developer, Order = 3, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = simpleWorkflowId, FromState = WorkflowState.QualityAssurance, ToState = WorkflowState.Completed, RequiredRole = TeamMemberRole.QualityAssurance, Order = 4, CreatedAt = DateTime.UtcNow }
        );

        // Seed default API keys
        context.ApiKeys.AddRange(
            new ApiKey
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Key = "sk-admin-" + Guid.NewGuid().ToString("N"),
                Type = ApiKeyType.Admin,
                Name = "Default Admin Key",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new ApiKey
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Key = "sk-openapi-" + Guid.NewGuid().ToString("N"),
                Type = ApiKeyType.OpenApi,
                Name = "Default OpenAPI Key",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        );

        await context.SaveChangesAsync();
    }
}