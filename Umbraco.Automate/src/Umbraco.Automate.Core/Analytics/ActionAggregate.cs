using Umbraco.Automate.Core.Actions;

namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// One action's step-run figures for a window.
/// </summary>
/// <param name="ActionAlias">The action's alias.</param>
/// <param name="Runs">Step runs that started in the window, except skipped ones.</param>
/// <param name="Completed">Completed step runs.</param>
/// <param name="Failed">Failed step runs.</param>
/// <param name="Rejected">Step runs whose approval decision was a refusal.</param>
/// <param name="Retries">The total retries across those step runs.</param>
/// <param name="AvgDurationTicks">Average duration, in ticks, of completed, failed and rejected step runs with a recorded duration; null when none.</param>
/// <param name="FailuresByCategory">Failed step runs per error category. A failure with no category counts as <see cref="StepRunErrorCategory.Unknown"/>.</param>
internal sealed record ActionAggregate(
    string ActionAlias,
    int Runs,
    int Completed,
    int Failed,
    int Rejected,
    int Retries,
    double? AvgDurationTicks,
    IReadOnlyDictionary<StepRunErrorCategory, int> FailuresByCategory);
