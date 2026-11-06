using Umbraco.Automate.Core.Runs;

namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// The number of runs with one status that started in one UTC quarter hour.
/// </summary>
/// <param name="QuarterStartUtc">The start of the quarter hour, as a UTC time.</param>
/// <param name="Status">The run status.</param>
/// <param name="Count">The number of runs.</param>
internal sealed record RunCountGroup(DateTime QuarterStartUtc, AutomationRunStatus Status, int Count);
