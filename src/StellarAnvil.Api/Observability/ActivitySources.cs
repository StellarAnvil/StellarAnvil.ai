using System.Diagnostics;

namespace StellarAnvil.Api.Observability;

/// <summary>
/// Activity sources for distributed tracing
/// </summary>
public static class ActivitySources
{
    public static readonly ActivitySource StellarAnvil = new("StellarAnvil.Api");
    public static readonly ActivitySource Workflow = new("StellarAnvil.Workflow");
    public static readonly ActivitySource AI = new("StellarAnvil.AI");
    public static readonly ActivitySource Database = new("StellarAnvil.Database");
}

/// <summary>
/// Extension methods for creating activities with common tags
/// </summary>
public static class ActivityExtensions
{
    public static Activity? StartWorkflowActivity(this ActivitySource source, string operationName, Guid? taskId = null, string? workflowName = null)
    {
        var activity = source.StartActivity(operationName);
        
        if (taskId.HasValue)
            activity?.SetTag("task.id", taskId.Value.ToString());
        
        if (!string.IsNullOrEmpty(workflowName))
            activity?.SetTag("workflow.name", workflowName);
        
        return activity;
    }

    public static Activity? StartAIActivity(this ActivitySource source, string operationName, string? model = null, string? role = null)
    {
        var activity = source.StartActivity(operationName);
        
        if (!string.IsNullOrEmpty(model))
            activity?.SetTag("ai.model", model);
        
        if (!string.IsNullOrEmpty(role))
            activity?.SetTag("ai.role", role);
        
        return activity;
    }

    public static Activity? StartDatabaseActivity(this ActivitySource source, string operationName, string? entityType = null)
    {
        var activity = source.StartActivity(operationName);
        
        if (!string.IsNullOrEmpty(entityType))
            activity?.SetTag("db.entity_type", entityType);
        
        return activity;
    }
}
