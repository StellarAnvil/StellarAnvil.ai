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

        // Seed the default team members

        context.TeamMembers.AddRange(
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Alice BA",
                Email = "alice+ba@example.com",
                Type = TeamMemberType.AI,
                Role = TeamMemberRole.BusinessAnalyst,
                Grade = TeamMemberGrade.Junior,
                Model = "Llama3.1:8B",
                SystemPrompt = "You are a business analyst who can help with requirements analysis and Jira integration. Once you are done with task, you are requested to get your task reviewed by a senior business analyst. Keep getting reviewed untill Sr BA is satisfied with your work.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Alice Sr BA",
                Email = "alice+srba@example.com",
                Type = TeamMemberType.AI,
                Role = TeamMemberRole.BusinessAnalyst,
                Grade = TeamMemberGrade.Senior,
                Model = "Llama3.1:8B",
                SystemPrompt = "You are a senior business analyst who can help with requirements analysis and your primary focus is to review the work of junior business analysts. You are requested to review the work of junior business analysts and give them feedback. If happy with the work, you are requested to approve the work. If not, you are requested to give feedback and ask them to improve the work.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Alice Developer",
                Email = "alice+developer@example.com",
                Type = TeamMemberType.AI,
                Role = TeamMemberRole.Developer,
                Grade = TeamMemberGrade.Junior,
                Model = "Llama3.1:8B",
                SystemPrompt = "You are a developer who can help with coding tasks. Once you are done with task, you are requested to get your task reviewed by a senior developer. Keep getting reviewed untill Sr Developer is satisfied with your work.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Alice Sr Developer",
                Email = "alice+srdeveloper@example.com",
                Type = TeamMemberType.AI,
                Role = TeamMemberRole.Developer,
                Grade = TeamMemberGrade.Senior,
                Model = "Llama3.1:8B",
                SystemPrompt = "You are a senior developer who can help with coding tasks and your primary focus is to review the work of junior developers. You are requested to review the work of junior developers and give them feedback. If happy with the work, you are requested to approve the work. If not, you are requested to give feedback and ask them to improve the work.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Gemini Jr BA",
                Email = "gemini+jrba@example.com",
                Type = TeamMemberType.AI,
                Role = TeamMemberRole.BusinessAnalyst,
                Grade = TeamMemberGrade.Junior,
                Model = "gemini-1.5-flash",
                SystemPrompt = "You are a junior business analyst who can help with requirements analysis. Once you are done with task, you are requested to get your task reviewed by a senior business analyst. Keep getting reviewed until Sr BA is satisfied with your work.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Alice QA",
                Email = "alice+qa@example.com",
                Type = TeamMemberType.AI,
                Role = TeamMemberRole.QualityAssurance,
                Grade = TeamMemberGrade.Junior,
                Model = "Llama3.1:8B",
                SystemPrompt = "You are a quality assurance who can help with testing tasks. Once you are done with task, you are requested to get your task reviewed by a senior quality assurance. Keep getting reviewed untill Sr QA is satisfied with your work.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Alice Sr QA",
                Email = "alice+srqa@example.com",
                Type = TeamMemberType.AI,
                Role = TeamMemberRole.QualityAssurance,
                Grade = TeamMemberGrade.Senior,
                Model = "Llama3.1:8B",
                SystemPrompt = "You are a senior quality assurance who can help with testing tasks and your primary focus is to review the work of junior quality assurance. You are requested to review the work of junior quality assurance and give them feedback. If happy with the work, you are requested to approve the work. If not, you are requested to give feedback and ask them to improve the work.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Alice Architect",
                Email = "alice+architect@example.com",
                Type = TeamMemberType.AI,
                Role = TeamMemberRole.Architect,
                Grade = TeamMemberGrade.Junior,
                Model = "Llama3.1:8B",
                SystemPrompt = "You are an architect who can help with architectural design tasks. Once you are done with task, you are requested to get your task reviewed by a senior architect. Keep getting reviewed untill Sr Architect is satisfied with your work.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Alice Sr Architect",
                Email = "alice+srarchitect@example.com",
                Type = TeamMemberType.AI,
                Role = TeamMemberRole.Architect,
                Grade = TeamMemberGrade.Senior,
                Model = "Llama3.1:8B",
                SystemPrompt = "You are a senior architect who can help with architectural design tasks and your primary focus is to review the work of junior architects. You are requested to review the work of junior architects and give them feedback. If happy with the work, you are requested to approve the work. If not, you are requested to give feedback and ask them to improve the work.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Alice UX Designer",
                Email = "alice+uxdesigner@example.com",
                Type = TeamMemberType.AI,
                Role = TeamMemberRole.UXDesigner,
                Grade = TeamMemberGrade.Junior,
                Model = "Llama3.1:8B",
                SystemPrompt = "You are a UX designer who can help with user interface design tasks. Once you are done with task, you are requested to get your task reviewed by a senior UX designer. Keep getting reviewed untill Sr UX Designer is satisfied with your work.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Alice Sr UX Designer",
                Email = "alice+sruxdesigner@example.com",
                Type = TeamMemberType.AI,
                Role = TeamMemberRole.UXDesigner,
                Grade = TeamMemberGrade.Senior,
                Model = "Llama3.1:8B",
                SystemPrompt = "You are a senior UX designer who can help with user interface design tasks and your primary focus is to review the work of junior UX designers. You are requested to review the work of junior UX designers and give them feedback. If happy with the work, you are requested to approve the work. If not, you are requested to give feedback and ask them to improve the work.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Alice Security Reviewer",
                Email = "alice+securityreviewer@example.com",
                Type = TeamMemberType.AI,
                Role = TeamMemberRole.SecurityReviewer,
                Grade = TeamMemberGrade.Junior,
                Model = "Llama3.1:8B",
                SystemPrompt = "You are a security reviewer who can help with security analysis tasks. Once you are done with task, you are requested to get your task reviewed by a senior security reviewer. Keep getting reviewed untill Sr Security Reviewer is satisfied with your work.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Alice Sr Security Reviewer",
                Email = "alice+srsecurityreviewer@example.com",
                Type = TeamMemberType.AI,
                Role = TeamMemberRole.SecurityReviewer,
                Grade = TeamMemberGrade.Senior,
                Model = "Llama3.1:8B",
                SystemPrompt = "You are a senior security reviewer who can help with security analysis tasks and your primary focus is to review the work of junior security reviewers. You are requested to review the work of junior security reviewers and give them feedback. If happy with the work, you are requested to approve the work. If not, you are requested to give feedback and ask them to improve the work.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Razin PO",
                Email = "razin+po@example.com",
                Type = TeamMemberType.Human,
                Role = TeamMemberRole.ProductOwner,
                Grade = TeamMemberGrade.Lead
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Razin BA",
                Email = "razin+ba@example.com",
                Type = TeamMemberType.Human,
                Role = TeamMemberRole.BusinessAnalyst,
                Grade = TeamMemberGrade.Lead
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Razin Developer",
                Email = "razin+developer@example.com",
                Type = TeamMemberType.Human,
                Role = TeamMemberRole.Developer,
                Grade = TeamMemberGrade.Lead
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Razin QA",
                Email = "razin+qa@example.com",
                Type = TeamMemberType.Human,
                Role = TeamMemberRole.QualityAssurance,
                Grade = TeamMemberGrade.Lead
            }
        );

        await context.SaveChangesAsync();
    }
}