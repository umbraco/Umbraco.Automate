namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// The run counts and the median run time for one window.
/// </summary>
/// <param name="Groups">Run counts by UTC quarter hour and status.</param>
/// <param name="MedianDurationMs">The median duration of finished runs with a recorded duration, or <c>null</c> when there are none.</param>
internal sealed record RunSummaryAggregates(IReadOnlyList<RunCountGroup> Groups, long? MedianDurationMs);
