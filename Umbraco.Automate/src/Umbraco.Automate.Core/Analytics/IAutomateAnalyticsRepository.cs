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
    /// Counts runs started in the window, grouped by UTC quarter hour and status, and gets the
    /// median duration, in milliseconds, of finished runs started in the window that have a
    /// recorded duration (<c>null</c> when there are none). One call, so the median's population
    /// always matches the groups.
    /// </summary>
    Task<RunSummaryAggregates> GetRunSummaryAggregatesAsync(
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

    /// <summary>
    /// Gets one row per action alias that has step runs started in the window. A step run belongs to
    /// the window its own start time falls in, not its run's. Skipped steps and steps that never
    /// started are left out.
    /// </summary>
    Task<IReadOnlyList<ActionAggregate>> GetActionAggregatesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlySet<Guid>? workspaceIds,
        CancellationToken cancellationToken = default);
}
