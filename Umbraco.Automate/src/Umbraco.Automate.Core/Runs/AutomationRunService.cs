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
}
