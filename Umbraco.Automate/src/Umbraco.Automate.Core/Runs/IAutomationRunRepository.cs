namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Repository for automation run persistence. Internal implementation detail of <c>IAutomationService</c>.
/// </summary>
internal interface IAutomationRunRepository
{
    /// <summary>
    /// Gets a run by its unique ID, including step runs.
    /// </summary>
    Task<AutomationRun?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paged runs for a specific automation.
    /// </summary>
    Task<(IEnumerable<AutomationRun> Items, int Total)> GetPagedByAutomationAsync(
        Guid automationId,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paged runs across all automations, newest first, optionally scoped to the given
    /// workspaces. Scoping is matched on each run's automation's <em>current</em> workspace
    /// (not the run's execution-time snapshot). Pass <c>null</c> for
    /// <paramref name="workspaceIds"/> to return runs from all workspaces.
    /// </summary>
    Task<(IReadOnlyList<AutomationRunListItem> Items, int Total)> GetPagedAsync(
        IReadOnlySet<Guid>? workspaceIds,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a run (insert or update). Does not cascade to step runs.
    /// </summary>
    Task<AutomationRun> SaveAsync(AutomationRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the WorkflowCore instance ID for a run. Scoped update — writes only that
    /// column so it cannot clobber a concurrent <see cref="RunFinalizer"/> write that
    /// may have happened between <c>StartWorkflow</c> returning and this call.
    /// </summary>
    Task SetWorkflowInstanceIdAsync(
        Guid runId,
        string workflowInstanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a step run (insert or update).
    /// </summary>
    Task<StepRun> SaveStepRunAsync(StepRun stepRun, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all runs for an automation (cascade deletes step runs).
    /// </summary>
    Task<int> DeleteByAutomationAsync(Guid automationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all completed runs older than the specified threshold (cascade deletes step runs).
    /// Running or pending runs are never deleted.
    /// </summary>
    /// <returns>The number of runs deleted.</returns>
    Task<int> DeleteRunsOlderThanAsync(DateTime threshold, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes excess completed runs per automation, keeping only the most recent N runs.
    /// Running or pending runs are never deleted.
    /// </summary>
    /// <returns>The number of runs deleted.</returns>
    Task<int> DeleteExcessRunsAsync(int maxRunsPerAutomation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of the most recent terminal run before the specified run for the same automation.
    /// </summary>
    Task<AutomationRunStatus?> GetPreviousTerminalRunStatusAsync(
        Guid automationId,
        Guid currentRunId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent terminal run statuses for an automation, newest first, capped at
    /// <paramref name="windowSize"/> and only including runs that started after <paramref name="since"/>.
    /// Used by the circuit breaker to derive consecutive-failure count and error rate without a
    /// persisted counter; <paramref name="since"/> is the per-automation window floor that
    /// advances on re-enable / re-publish so stale failures don't re-trip the breaker.
    /// </summary>
    Task<IReadOnlyList<AutomationRunStatus>> GetRecentTerminalStatusesAsync(
        Guid automationId,
        int windowSize,
        DateTime since,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets step runs matching the given action alias and status, along with their parent runs.
    /// </summary>
    Task<IReadOnlyList<(AutomationRun Run, StepRun StepRun)>> GetStepRunsByActionAndStatusAsync(
        string actionAlias,
        StepRunStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets run counts grouped by status, scoped to the given workspaces (matched on each run's
    /// automation's current workspace). Pass <c>null</c> for <paramref name="workspaceIds"/> to
    /// count across all workspaces.
    /// </summary>
    Task<Dictionary<AutomationRunStatus, int>> GetRunCountsByStatusAsync(
        IReadOnlySet<Guid>? workspaceIds = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets run counts grouped by automation, scoped to the given workspaces (matched on each run's
    /// automation's current workspace). Pass <c>null</c> for <paramref name="workspaceIds"/> to
    /// count across all workspaces.
    /// </summary>
    Task<IReadOnlyList<AutomationRunCount>> GetRunCountsByAutomationAsync(
        IReadOnlySet<Guid>? workspaceIds = null,
        DateTime? from = null,
        DateTime? to = null,
        int take = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of runs started since the specified time for an automation.
    /// </summary>
    Task<int> GetRecentRunCountAsync(
        Guid automationId,
        DateTime since,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of currently running or pending runs for an automation.
    /// </summary>
    Task<int> GetConcurrentRunCountAsync(
        Guid automationId,
        CancellationToken cancellationToken = default);
}
