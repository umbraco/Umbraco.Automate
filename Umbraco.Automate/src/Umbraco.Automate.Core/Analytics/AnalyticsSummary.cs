namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// The headline numbers and the daily chart for a range, with an optional comparison.
/// </summary>
public sealed record AnalyticsSummary
{
    /// <summary>Gets the range asked for.</summary>
    public required AnalyticsPeriod Current { get; init; }

    /// <summary>Gets the comparison range, or <c>null</c> when none was asked for or its runs have been deleted by run cleanup.</summary>
    public AnalyticsPeriod? Compare { get; init; }

    /// <summary>Gets the runs suspended right now, whatever the range.</summary>
    public int WaitingForApproval { get; init; }

    /// <summary>Gets the first day fully kept by age-based run cleanup, or <c>null</c> when that cleanup is off.</summary>
    public DateOnly? RetainedFrom { get; init; }
}
