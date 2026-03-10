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
    /// Saves a step run (insert or update).
    /// </summary>
    Task<StepRun> SaveStepRunAsync(StepRun stepRun, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all runs for an automation (cascade deletes step runs).
    /// </summary>
    Task<int> DeleteByAutomationAsync(Guid automationId, CancellationToken cancellationToken = default);
}
