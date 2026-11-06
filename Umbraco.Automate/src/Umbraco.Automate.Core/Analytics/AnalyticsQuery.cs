namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// What an analytics call asks for: a range of local days, an optional comparison range of the same
/// length, and the workspaces to scope to.
/// </summary>
public sealed record AnalyticsQuery
{
    /// <summary>Gets the first day, in <see cref="TimeZoneId"/>.</summary>
    public required DateOnly From { get; init; }

    /// <summary>Gets the last day, inclusive.</summary>
    public required DateOnly To { get; init; }

    /// <summary>Gets the first day of the comparison range; needs <see cref="CompareTo"/>.</summary>
    public DateOnly? CompareFrom { get; init; }

    /// <summary>Gets the last day of the comparison range, inclusive; needs <see cref="CompareFrom"/>.</summary>
    public DateOnly? CompareTo { get; init; }

    /// <summary>Gets the IANA name of the time zone the days are in, such as <c>Europe/Copenhagen</c>.</summary>
    public required string TimeZoneId { get; init; }

    /// <summary>Gets the workspaces to count, matched on the automation's current workspace; <c>null</c> means all.</summary>
    public IReadOnlySet<Guid>? WorkspaceIds { get; init; }
}
