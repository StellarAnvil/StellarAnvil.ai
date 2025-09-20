using Microsoft.EntityFrameworkCore;
using StellarAnvil.Domain.Entities;
using StellarAnvil.Domain.Enums;

namespace StellarAnvil.Infrastructure.Data;

public class StellarAnvilDbContext : DbContext
{
    public StellarAnvilDbContext(DbContextOptions<StellarAnvilDbContext> options) : base(options)
    {
    }

    public DbSet<TeamMember> TeamMembers { get; set; }
    public DbSet<Domain.Entities.Task> Tasks { get; set; }
    public DbSet<TaskHistory> TaskHistories { get; set; }
    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<WorkflowTransition> WorkflowTransitions { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    public DbSet<McpConfiguration> McpConfigurations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TeamMember configuration
        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Role).HasConversion<int>();
            entity.Property(e => e.Grade).HasConversion<int>();
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.SystemPromptFile).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Name);
            
            entity.HasOne(e => e.CurrentTask)
                .WithMany()
                .HasForeignKey(e => e.CurrentTaskId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Task configuration
        modelBuilder.Entity<Domain.Entities.Task>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.CurrentState).HasConversion<int>();
            
            entity.HasOne(e => e.Assignee)
                .WithMany(tm => tm.AssignedTasks)
                .HasForeignKey(e => e.AssigneeId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.HasOne(e => e.Workflow)
                .WithMany(w => w.Tasks)
                .HasForeignKey(e => e.WorkflowId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // TaskHistory configuration
        modelBuilder.Entity<TaskHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FromState).HasConversion<int>();
            entity.Property(e => e.ToState).HasConversion<int>();
            entity.Property(e => e.Action).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            
            entity.HasOne(e => e.Task)
                .WithMany(t => t.TaskHistories)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.TeamMember)
                .WithMany(tm => tm.TaskHistories)
                .HasForeignKey(e => e.TeamMemberId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Workflow configuration
        modelBuilder.Entity<Workflow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // WorkflowTransition configuration
        modelBuilder.Entity<WorkflowTransition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FromState).HasConversion<int>();
            entity.Property(e => e.ToState).HasConversion<int>();
            entity.Property(e => e.RequiredRole).HasConversion<int>();
            
            entity.HasOne(e => e.Workflow)
                .WithMany(w => w.Transitions)
                .HasForeignKey(e => e.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => new { e.WorkflowId, e.FromState, e.ToState }).IsUnique();
        });

        // ApiKey configuration
        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Key).IsUnique();
        });

        // McpConfiguration configuration
        modelBuilder.Entity<McpConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ApiKey).IsRequired().HasMaxLength(500);
            entity.Property(e => e.BaseUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Configuration).HasColumnType("jsonb");
            entity.HasIndex(e => new { e.Name, e.Type }).IsUnique();
        });

        // Seed default workflows
        SeedDefaultWorkflows(modelBuilder);
    }

    private static void SeedDefaultWorkflows(ModelBuilder modelBuilder)
    {
        var fullWorkflowId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var standardWorkflowId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var simpleWorkflowId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        // Full workflow: PO -> BA -> Architect -> UX -> Dev -> QA -> Done
        modelBuilder.Entity<Workflow>().HasData(
            new Workflow
            {
                Id = fullWorkflowId,
                Name = "Full SDLC Workflow",
                Description = "Complete software development lifecycle with all phases",
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );

        modelBuilder.Entity<WorkflowTransition>().HasData(
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = fullWorkflowId, FromState = WorkflowState.Planning, ToState = WorkflowState.RequirementsAnalysis, RequiredRole = TeamMemberRole.ProductOwner, Order = 1, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = fullWorkflowId, FromState = WorkflowState.RequirementsAnalysis, ToState = WorkflowState.ArchitecturalDesign, RequiredRole = TeamMemberRole.BusinessAnalyst, Order = 2, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = fullWorkflowId, FromState = WorkflowState.ArchitecturalDesign, ToState = WorkflowState.UXDesign, RequiredRole = TeamMemberRole.Architect, Order = 3, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = fullWorkflowId, FromState = WorkflowState.UXDesign, ToState = WorkflowState.Development, RequiredRole = TeamMemberRole.UXDesigner, Order = 4, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = fullWorkflowId, FromState = WorkflowState.Development, ToState = WorkflowState.QualityAssurance, RequiredRole = TeamMemberRole.Developer, Order = 5, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = fullWorkflowId, FromState = WorkflowState.QualityAssurance, ToState = WorkflowState.Completed, RequiredRole = TeamMemberRole.QualityAssurance, Order = 6, CreatedAt = DateTime.UtcNow }
        );

        // Standard workflow: PO -> BA -> Architect -> Dev -> QA -> Done
        modelBuilder.Entity<Workflow>().HasData(
            new Workflow
            {
                Id = standardWorkflowId,
                Name = "Standard SDLC Workflow",
                Description = "Standard software development lifecycle without UX design",
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );

        modelBuilder.Entity<WorkflowTransition>().HasData(
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = standardWorkflowId, FromState = WorkflowState.Planning, ToState = WorkflowState.RequirementsAnalysis, RequiredRole = TeamMemberRole.ProductOwner, Order = 1, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = standardWorkflowId, FromState = WorkflowState.RequirementsAnalysis, ToState = WorkflowState.ArchitecturalDesign, RequiredRole = TeamMemberRole.BusinessAnalyst, Order = 2, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = standardWorkflowId, FromState = WorkflowState.ArchitecturalDesign, ToState = WorkflowState.Development, RequiredRole = TeamMemberRole.Architect, Order = 3, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = standardWorkflowId, FromState = WorkflowState.Development, ToState = WorkflowState.QualityAssurance, RequiredRole = TeamMemberRole.Developer, Order = 4, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = standardWorkflowId, FromState = WorkflowState.QualityAssurance, ToState = WorkflowState.Completed, RequiredRole = TeamMemberRole.QualityAssurance, Order = 5, CreatedAt = DateTime.UtcNow }
        );

        // Simple workflow: PO -> BA -> Dev -> QA -> Done
        modelBuilder.Entity<Workflow>().HasData(
            new Workflow
            {
                Id = simpleWorkflowId,
                Name = "Simple SDLC Workflow",
                Description = "Simplified software development lifecycle for small changes",
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );

        modelBuilder.Entity<WorkflowTransition>().HasData(
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = simpleWorkflowId, FromState = WorkflowState.Planning, ToState = WorkflowState.RequirementsAnalysis, RequiredRole = TeamMemberRole.ProductOwner, Order = 1, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = simpleWorkflowId, FromState = WorkflowState.RequirementsAnalysis, ToState = WorkflowState.Development, RequiredRole = TeamMemberRole.BusinessAnalyst, Order = 2, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = simpleWorkflowId, FromState = WorkflowState.Development, ToState = WorkflowState.QualityAssurance, RequiredRole = TeamMemberRole.Developer, Order = 3, CreatedAt = DateTime.UtcNow },
            new WorkflowTransition { Id = Guid.NewGuid(), WorkflowId = simpleWorkflowId, FromState = WorkflowState.QualityAssurance, ToState = WorkflowState.Completed, RequiredRole = TeamMemberRole.QualityAssurance, Order = 4, CreatedAt = DateTime.UtcNow }
        );

        // Seed default API keys
        modelBuilder.Entity<ApiKey>().HasData(
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
    }
}
