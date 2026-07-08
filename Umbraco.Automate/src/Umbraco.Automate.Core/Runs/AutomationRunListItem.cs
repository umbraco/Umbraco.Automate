namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Lightweight projection of an automation run for cross-automation list views (e.g. the
/// runs dashboard). Includes the parent automation's current name and excludes step runs.
/// </summary>
public sealed class AutomationRunListItem
{
    /// <summary>The run ID.</summary>
    public Guid Id { get; set; }

    /// <summary>The automation ID.</summary>
    public Guid AutomationId { get; set; }

    /// <summary>The current name of the parent automation.</summary>
    public required string AutomationName { get; set; }

    /// <summary>The automation version at the time of the run.</summary>
    public int AutomationVersion { get; set; }

    /// <summary>The execution status.</summary>
    public AutomationRunStatus Status { get; set; }

    /// <summary>When the run started.</summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>When the run completed.</summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>What initiated the run.</summary>
    public required string InitiatedBy { get; set; }

    /// <summary>Optional correlation ID.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; set; }
}
