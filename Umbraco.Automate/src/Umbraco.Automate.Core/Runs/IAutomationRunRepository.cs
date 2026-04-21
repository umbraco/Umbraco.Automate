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
    /// Gets step runs matching the given action alias and status, along with their parent runs.
    /// </summary>
    Task<IReadOnlyList<(AutomationRun Run, StepRun StepRun)>> GetStepRunsByActionAndStatusAsync(
        string actionAlias,
        StepRunStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets run counts grouped by status within the specified filters.
    /// </summary>
    Task<Dictionary<AutomationRunStatus, int>> GetRunCountsByStatusAsync(
        Guid? workspaceId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets run counts grouped by automation within the specified filters.
    /// </summary>
    Task<IReadOnlyList<AutomationRunCount>> GetRunCountsByAutomationAsync(
        Guid? workspaceId = null,
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
