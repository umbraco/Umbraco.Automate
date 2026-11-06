namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// The run figures for one range.
/// </summary>
public sealed record AnalyticsPeriod
{
    /// <summary>Gets the number of runs that started in the range.</summary>
    public int Runs { get; init; }

    /// <summary>Gets the completed runs.</summary>
    public int Completed { get; init; }

    /// <summary>Gets the runs a person refused.</summary>
    public int Rejected { get; init; }

    /// <summary>Gets the cancelled runs.</summary>
    public int Cancelled { get; init; }

    /// <summary>Gets the failed runs.</summary>
    public int Failed { get; init; }

    /// <summary>Gets the pending, running and suspended runs.</summary>
    public int InProgress { get; init; }

    /// <summary>Gets (completed + rejected) over all finished runs, 0 to 1; <c>null</c> when none finished.</summary>
    public decimal? SuccessRate { get; init; }

    /// <summary>Gets the median run time of finished runs, or <c>null</c> when there are none.</summary>
    public long? MedianDurationMs { get; init; }

    /// <summary>Gets every day of the range, zeros included.</summary>
    public required IReadOnlyList<AnalyticsDay> Days { get; init; }
}
