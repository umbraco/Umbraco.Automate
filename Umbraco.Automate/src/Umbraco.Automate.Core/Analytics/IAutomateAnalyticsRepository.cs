using Umbraco.Automate.Core.Runs;

namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// Read-only run queries behind the analytics screens. Every query groups in the database and
/// never loads run rows. Each takes the workspaces to scope to, matched on the automation's
/// <em>current</em> workspace; <c>null</c> means all workspaces. Windows are <c>[fromUtc, toUtc)</c>
/// on the run's start time, and runs that never started are left out.
/// </summary>
internal interface IAutomateAnalyticsRepository
{
    /// <summary>
    /// Counts runs started in the window, grouped by UTC quarter hour and status.
    /// </summary>
    Task<IReadOnlyList<RunCountGroup>> GetRunCountGroupsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlySet<Guid>? workspaceIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the median duration, in milliseconds, of finished runs started in the window that have a
    /// recorded duration; <c>null</c> when there are none.
    /// </summary>
    Task<long?> GetMedianRunDurationMsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlySet<Guid>? workspaceIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the runs that are suspended right now, whenever they started.
    /// </summary>
    Task<int> GetSuspendedRunCountAsync(
        IReadOnlySet<Guid>? workspaceIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one row per automation that has runs started in the window. Runs of a deleted
    /// automation have no row.
    /// </summary>
    Task<IReadOnlyList<AutomationAggregate>> GetAutomationAggregatesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlySet<Guid>? workspaceIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The number of runs with one status that started in one UTC quarter hour.
/// </summary>
/// <param name="QuarterStartUtc">The start of the quarter hour, as a UTC time.</param>
/// <param name="Status">The run status.</param>
/// <param name="Count">The number of runs.</param>
internal sealed record RunCountGroup(DateTime QuarterStartUtc, AutomationRunStatus Status, int Count);

/// <summary>
/// One automation's run figures for a window.
/// </summary>
/// <param name="AutomationId">The automation's ID.</param>
/// <param name="Name">The automation's current name.</param>
/// <param name="WorkspaceId">The automation's current workspace.</param>
/// <param name="Runs">All runs that started in the window.</param>
/// <param name="Succeeded">Completed plus rejected runs. A rejection is a person saying no, not a fault.</param>
/// <param name="Failed">Failed runs.</param>
/// <param name="Finished">Completed, rejected, cancelled and failed runs.</param>
/// <param name="AvgDurationMs">Average duration of finished runs with a recorded duration; null when none.</param>
/// <param name="LastStartedUtc">When the most recent run started.</param>
internal sealed record AutomationAggregate(
    Guid AutomationId,
    string Name,
    Guid WorkspaceId,
    int Runs,
    int Succeeded,
    int Failed,
    int Finished,
    double? AvgDurationMs,
    DateTime LastStartedUtc);
