namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Default implementation of <see cref="IAutomationRunService"/>.
/// </summary>
internal sealed class AutomationRunService : IAutomationRunService
{
    private readonly IAutomationRunRepository _runRepository;

    public AutomationRunService(IAutomationRunRepository runRepository)
    {
        _runRepository = runRepository;
    }

    public Task<AutomationRun?> GetRunAsync(Guid id, CancellationToken cancellationToken = default)
        => _runRepository.GetAsync(id, cancellationToken);

    public Task<(IEnumerable<AutomationRun> Items, int Total)> GetRunsByAutomationPagedAsync(
        Guid automationId,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
        => _runRepository.GetPagedByAutomationAsync(automationId, skip, take, cancellationToken);

    public Task<AutomationRunStatus?> GetPreviousTerminalRunStatusAsync(
        Guid automationId,
        Guid currentRunId,
        CancellationToken cancellationToken = default)
        => _runRepository.GetPreviousTerminalRunStatusAsync(automationId, currentRunId, cancellationToken);

    public Task<IReadOnlyList<(AutomationRun Run, StepRun StepRun)>> GetStepRunsByStatusAsync(
        string actionAlias,
        StepRunStatus status,
        CancellationToken cancellationToken = default)
        => _runRepository.GetStepRunsByActionAndStatusAsync(actionAlias, status, cancellationToken);

    public async Task<RunSummary> GetRunSummaryAsync(
        Guid? workspaceId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var byStatus = await _runRepository.GetRunCountsByStatusAsync(workspaceId, from, to, cancellationToken);
        var totalRuns = byStatus.Values.Sum();

        byStatus.TryGetValue(AutomationRunStatus.Completed, out var completedCount);

        var successRate = totalRuns > 0
            ? Math.Round((decimal)completedCount / totalRuns, 4)
            : 0m;

        return new RunSummary
        {
            TotalRuns = totalRuns,
            ByStatus = byStatus,
            SuccessRate = successRate,
        };
    }

    public Task<IReadOnlyList<AutomationRunCount>> GetRunCountsByAutomationAsync(
        Guid? workspaceId = null,
        DateTime? from = null,
        DateTime? to = null,
        int take = 10,
        CancellationToken cancellationToken = default)
        => _runRepository.GetRunCountsByAutomationAsync(workspaceId, from, to, take, cancellationToken);
}
