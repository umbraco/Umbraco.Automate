namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// The rows of an analytics table for a range, and for the comparison range when there is one.
/// </summary>
/// <typeparam name="TRow">The row type.</typeparam>
public sealed record AnalyticsRows<TRow>
{
    /// <summary>Gets the rows for the range asked for.</summary>
    public required IReadOnlyList<TRow> Items { get; init; }

    /// <summary>Gets the rows for the comparison range, or <c>null</c> when none was asked for or its runs have been deleted by run cleanup.</summary>
    public IReadOnlyList<TRow>? CompareItems { get; init; }
}
