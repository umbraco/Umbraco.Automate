namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// The runs that started on one local day, by outcome.
/// </summary>
public sealed record AnalyticsDay
{
    /// <summary>Gets the local day.</summary>
    public required DateOnly Date { get; init; }

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
}
