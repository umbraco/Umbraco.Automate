using Umbraco.Automate.Core.Actions;

namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// One action's step-run figures for a range.
/// </summary>
public sealed record ActionAnalyticsRow
{
    /// <summary>Gets the action's alias.</summary>
    public required string ActionAlias { get; init; }

    /// <summary>Gets the step runs of the action that started in the range.</summary>
    public int Runs { get; init; }

    /// <summary>Gets the failed step runs.</summary>
    public int Failed { get; init; }

    /// <summary>Gets the total retries.</summary>
    public int Retries { get; init; }

    /// <summary>Gets failed over (completed + failed + rejected) step runs, 0 to 1; <c>null</c> when none of those ran.</summary>
    public decimal? FailureRate { get; init; }

    /// <summary>Gets the average step run time, or <c>null</c> when none has a recorded duration.</summary>
    public long? AvgDurationMs { get; init; }

    /// <summary>Gets the error category with the most failures (the lower value on a tie), or <c>null</c> when nothing failed.</summary>
    public StepRunErrorCategory? TopErrorCategory { get; init; }
}
