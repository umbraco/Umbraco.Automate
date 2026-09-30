using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;

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
    /// Gets the recorded input and output of a single step run, prepared for display, or
    /// <c>null</c> when the step run does not exist or does not belong to <paramref name="runId"/>.
    /// The values are masked (sensitive keys, and fields the action's settings schema marks
    /// sensitive), pretty-printed and truncated to a maximum length — they are safe to show to a
    /// user but are not the stored payloads. The caller remains responsible for authorizing access
    /// to the run's workspace.
    /// </summary>
    // TODO (V19): Remove the default implementation.
    // The default keeps existing implementations of this interface compiling. It resolves the
    // internal repository and sanitizer through StaticServiceProvider, which is temporary and
    // goes with the default implementation.
    async Task<StepRunData?> GetStepRunDataAsync(Guid runId, Guid stepRunId, CancellationToken cancellationToken = default)
    {
        var repository = StaticServiceProvider.Instance.GetRequiredService<IAutomationRunRepository>();
        var sanitizer = StaticServiceProvider.Instance.GetRequiredService<IRunDataSanitizer>();

        var stored = await repository.GetStepRunDataAsync(runId, stepRunId, cancellationToken);
        return stored is null ? null : StepRunData.Create(stored, sanitizer);
    }

    /// <summary>
    /// Gets the recorded trigger data of a run, prepared for display, or <c>null</c> when the run
    /// does not exist. The value is masked, pretty-printed and truncated to a maximum length — it is
    /// safe to show to a user but is not the stored payload. The caller remains responsible for
    /// authorizing access to the run's workspace.
    /// </summary>
    // TODO (V19): Remove the default implementation.
    // The default keeps existing implementations of this interface compiling. It resolves the
    // internal repository and sanitizer through StaticServiceProvider, which is temporary and
    // goes with the default implementation.
    async Task<RunTriggerData?> GetTriggerDataAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var repository = StaticServiceProvider.Instance.GetRequiredService<IAutomationRunRepository>();
        var sanitizer = StaticServiceProvider.Instance.GetRequiredService<IRunDataSanitizer>();

        var stored = await repository.GetTriggerDataAsync(runId, cancellationToken);
        return stored is null ? null : RunTriggerData.Create(stored, sanitizer);
    }

    /// <summary>
    /// Gets paged runs for a specific automation.
    /// </summary>
    Task<(IEnumerable<AutomationRun> Items, int Total)> GetRunsByAutomationPagedAsync(
        Guid automationId,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paged runs across all automations, newest first, optionally scoped to the given
    /// workspaces (matched on each run's automation's current workspace). Pass <c>null</c>
    /// for <paramref name="workspaceIds"/> to return runs from all workspaces.
    /// </summary>
    Task<(IReadOnlyList<AutomationRunListItem> Items, int Total)> GetRunsPagedAsync(
        IReadOnlySet<Guid>? workspaceIds,
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
    /// Gets the most recent terminal run statuses for an automation, newest first, capped at
    /// <paramref name="windowSize"/> and only including runs started after <paramref name="since"/>.
    /// Used by the circuit breaker; <paramref name="since"/> is the per-automation window floor
    /// that advances on re-enable / re-publish.
    /// </summary>
    Task<IReadOnlyList<AutomationRunStatus>> GetRecentTerminalStatusesAsync(
        Guid automationId,
        int windowSize,
        DateTime since,
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
    /// Gets a summary of run statistics, scoped to the given workspaces (matched on each run's
    /// automation's current workspace). Pass <c>null</c> for <paramref name="workspaceIds"/> to
    /// summarise across all workspaces.
    /// </summary>
    Task<RunSummary> GetRunSummaryAsync(
        IReadOnlySet<Guid>? workspaceIds = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets run counts grouped by automation, ordered by total runs descending, scoped to the
    /// given workspaces (matched on each run's automation's current workspace). Pass <c>null</c>
    /// for <paramref name="workspaceIds"/> to count across all workspaces.
    /// </summary>
    Task<IReadOnlyList<AutomationRunCount>> GetRunCountsByAutomationAsync(
        IReadOnlySet<Guid>? workspaceIds = null,
        DateTime? from = null,
        DateTime? to = null,
        int take = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspends a running workflow, pausing it until <see cref="ResumeRunAsync"/> is called.
    /// </summary>
    Task<RunLifecycleResult> SuspendRunAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a suspended workflow, returning it to the running state.
    /// </summary>
    Task<RunLifecycleResult> ResumeRunAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates a running or suspended workflow. The run is recorded as Cancelled.
    /// </summary>
    Task<RunLifecycleResult> TerminateRunAsync(Guid runId, CancellationToken cancellationToken = default);
}
