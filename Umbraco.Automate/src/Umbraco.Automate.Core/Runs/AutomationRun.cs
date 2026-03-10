using Umbraco.Automate.Core.Models;

namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// A single execution of an automation, with a full step-by-step audit trail.
/// </summary>
public sealed class AutomationRun : IAutomateEntity
{
    /// <inheritdoc />
    public Guid Id { get; internal set; }

    /// <summary>
    /// Gets or sets the automation that was executed.
    /// </summary>
    public required Guid AutomationId { get; init; }

    /// <summary>
    /// Gets or sets the automation version that was executing (snapshot).
    /// </summary>
    public required int AutomationVersion { get; init; }

    /// <summary>
    /// Gets or sets the execution status.
    /// </summary>
    public AutomationRunStatus Status { get; set; } = AutomationRunStatus.Pending;

    /// <summary>
    /// Gets or sets the UTC time the run started.
    /// </summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the run completed.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the serialised trigger data (JSON).
    /// </summary>
    public string? TriggerData { get; set; }

    /// <summary>
    /// Gets or sets what initiated the run (e.g. "user", "system", "webhook", "ai-agent").
    /// </summary>
    public required string InitiatedBy { get; init; }

    /// <summary>
    /// Gets or sets an optional correlation ID for tracing.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the error message if the run failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the step runs within this automation run.
    /// </summary>
    public IList<StepRun> StepRuns { get; set; } = [];
}
