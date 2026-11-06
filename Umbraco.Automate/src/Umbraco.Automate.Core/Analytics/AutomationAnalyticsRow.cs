namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// One automation's run figures for a range.
/// </summary>
public sealed record AutomationAnalyticsRow
{
    /// <summary>Gets the automation's ID.</summary>
    public required Guid AutomationId { get; init; }

    /// <summary>Gets the automation's current name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the name of the automation's current workspace, or an empty string when that workspace no longer exists.</summary>
    public required string WorkspaceName { get; init; }

    /// <summary>Gets the runs that started in the range.</summary>
    public int Runs { get; init; }

    /// <summary>Gets the failed runs.</summary>
    public int Failed { get; init; }

    /// <summary>Gets (completed + rejected) over finished runs, 0 to 1; <c>null</c> when none finished.</summary>
    public decimal? SuccessRate { get; init; }

    /// <summary>Gets the average run time of finished runs, or <c>null</c> when none has a recorded duration.</summary>
    public long? AvgDurationMs { get; init; }

    /// <summary>Gets when the latest run in the range started.</summary>
    public DateTime LastRunUtc { get; init; }
}
