using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Runs;

namespace Umbraco.Automate.Web.Api.Management.Run.Models;

/// <summary>
/// Response model for an automation run.
/// </summary>
public sealed class AutomationRunResponseModel
{
    /// <summary>The run ID.</summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>The automation ID.</summary>
    [Required]
    public Guid AutomationId { get; set; }

    /// <summary>The automation version at the time of the run.</summary>
    public int AutomationVersion { get; set; }

    /// <summary>The execution status.</summary>
    [Required]
    public AutomationRunStatus Status { get; set; }

    /// <summary>When the run started.</summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>When the run completed.</summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>What initiated the run.</summary>
    [Required]
    public string InitiatedBy { get; set; } = string.Empty;

    /// <summary>Optional correlation ID.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; set; }

    /// <summary>The step runs within this automation run.</summary>
    public IList<StepRunResponseModel> StepRuns { get; set; } = [];
}

/// <summary>
/// Response model for a step run within an automation run.
/// </summary>
public sealed class StepRunResponseModel
{
    /// <summary>The step run ID.</summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>The step ID from the automation definition.</summary>
    [Required]
    public Guid StepId { get; set; }

    /// <summary>The action alias that was executed.</summary>
    [Required]
    public string ActionAlias { get; set; } = string.Empty;

    /// <summary>The execution status.</summary>
    [Required]
    public StepRunStatus Status { get; set; }

    /// <summary>When the step started.</summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>When the step completed.</summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; set; }

    /// <summary>Retry count.</summary>
    public int RetryCount { get; set; }

    /// <summary>Duration in milliseconds.</summary>
    public double? DurationMs { get; set; }

    /// <summary>The log entries recorded by the action during execution.</summary>
    public IList<StepRunLogEntryResponseModel> LogEntries { get; set; } = [];
}

/// <summary>
/// Response model for a single log entry recorded by an action during execution.
/// </summary>
public sealed class StepRunLogEntryResponseModel
{
    /// <summary>When the entry was recorded.</summary>
    [Required]
    public DateTime TimestampUtc { get; set; }

    /// <summary>The severity of the entry.</summary>
    [Required]
    public ActionLogLevel Level { get; set; }

    /// <summary>The log message text.</summary>
    [Required]
    public string Message { get; set; } = string.Empty;
}
