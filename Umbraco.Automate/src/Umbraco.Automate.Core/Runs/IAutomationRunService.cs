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

    /// <summary>
    /// Gets the status of the most recent terminal run before the specified run for the same automation.
    /// Used to determine recovery notifications.
    /// </summary>
    /// <returns>The previous run status, or null if no prior terminal run exists.</returns>
    Task<AutomationRunStatus?> GetPreviousTerminalRunStatusAsync(
        Guid automationId,
        Guid currentRunId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets step runs with a specific action alias and status, along with their parent runs.
    /// Used to find pending approval steps across all automations.
    /// </summary>
    Task<IReadOnlyList<(AutomationRun Run, StepRun StepRun)>> GetStepRunsByStatusAsync(
        string actionAlias,
        StepRunStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a summary of run statistics.
    /// </summary>
    Task<RunSummary> GetRunSummaryAsync(
        Guid? workspaceId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets run counts grouped by automation, ordered by total runs descending.
    /// </summary>
    Task<IReadOnlyList<AutomationRunCount>> GetRunCountsByAutomationAsync(
        Guid? workspaceId = null,
        DateTime? from = null,
        DateTime? to = null,
        int take = 10,
        CancellationToken cancellationToken = default);
}
