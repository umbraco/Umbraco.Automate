namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Service for querying automation run history.
/// </summary>
public interface IAutomationRunService
{
    /// <summary>
    /// Gets a run by its unique ID, including step runs.
    /// </summary>
    Task<AutomationRun?> GetRunAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paged runs for a specific automation.
    /// </summary>
    Task<(IEnumerable<AutomationRun> Items, int Total)> GetRunsByAutomationPagedAsync(
        Guid automationId,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);
}
