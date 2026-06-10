using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Models;

namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// The execution record for a single step within an automation run.
/// </summary>
public sealed class StepRun : IAutomateEntity
{
    /// <inheritdoc />
    public Guid Id { get; internal set; }

    /// <summary>
    /// Gets or sets the parent run ID.
    /// </summary>
    public required Guid RunId { get; init; }

    /// <summary>
    /// Gets or sets the step ID from the automation definition.
    /// </summary>
    public required Guid StepId { get; init; }

    /// <summary>
    /// Gets or sets the action alias that was executed.
    /// </summary>
    public required string ActionAlias { get; init; }

    /// <summary>
    /// Gets or sets the execution status.
    /// </summary>
    public StepRunStatus Status { get; set; } = StepRunStatus.Pending;

    /// <summary>
    /// Gets or sets the UTC time the step started.
    /// </summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the step completed.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the serialised input data (JSON).
    /// </summary>
    public string? InputData { get; set; }

    /// <summary>
    /// Gets or sets the serialised output data (JSON).
    /// </summary>
    public string? OutputData { get; set; }

    /// <summary>
    /// Gets or sets the error message if the step failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the error category if the step failed.
    /// </summary>
    public StepRunErrorCategory? ErrorCategory { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the execution duration.
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Gets or sets the branch outcome that was selected (for If/Switch control flow steps).
    /// </summary>
    public string? BranchOutcome { get; set; }

    /// <summary>
    /// Gets or sets the iteration index (for ForEach/While container steps).
    /// </summary>
    public int? IterationIndex { get; set; }

    /// <summary>
    /// Gets or sets the total iteration count (for ForEach/While container steps).
    /// </summary>
    public int? IterationTotal { get; set; }
}
