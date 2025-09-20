using System.Diagnostics.Metrics;

namespace StellarAnvil.Api.Observability;

/// <summary>
/// Custom metrics for StellarAnvil application
/// </summary>
public static class Metrics
{
    private static readonly Meter _meter = new("StellarAnvil.Api");

    // Counters
    public static readonly Counter<long> TasksCreated = _meter.CreateCounter<long>(
        "stellar_anvil_tasks_created_total",
        "Total number of tasks created");

    public static readonly Counter<long> TasksCompleted = _meter.CreateCounter<long>(
        "stellar_anvil_tasks_completed_total",
        "Total number of tasks completed");

    public static readonly Counter<long> WorkflowTransitions = _meter.CreateCounter<long>(
        "stellar_anvil_workflow_transitions_total",
        "Total number of workflow state transitions");

    public static readonly Counter<long> ChatCompletions = _meter.CreateCounter<long>(
        "stellar_anvil_chat_completions_total",
        "Total number of chat completions processed");

    public static readonly Counter<long> ApiKeyUsage = _meter.CreateCounter<long>(
        "stellar_anvil_api_key_usage_total",
        "Total number of API key authentications");

    // Histograms
    public static readonly Histogram<double> ChatCompletionDuration = _meter.CreateHistogram<double>(
        "stellar_anvil_chat_completion_duration_seconds",
        "Duration of chat completion requests in seconds");

    public static readonly Histogram<double> WorkflowTransitionDuration = _meter.CreateHistogram<double>(
        "stellar_anvil_workflow_transition_duration_seconds",
        "Duration of workflow transitions in seconds");

    // Gauges (using UpDownCounter as approximation)
    public static readonly UpDownCounter<long> ActiveTasks = _meter.CreateUpDownCounter<long>(
        "stellar_anvil_active_tasks",
        "Number of currently active tasks");

    public static readonly UpDownCounter<long> AvailableTeamMembers = _meter.CreateUpDownCounter<long>(
        "stellar_anvil_available_team_members",
        "Number of available team members");

    public static readonly UpDownCounter<long> ActiveConnections = _meter.CreateUpDownCounter<long>(
        "stellar_anvil_active_connections",
        "Number of active API connections");
}
