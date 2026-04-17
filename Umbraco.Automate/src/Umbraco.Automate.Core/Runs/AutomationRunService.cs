using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Default implementation of <see cref="IAutomationRunService"/>.
/// </summary>
internal sealed class AutomationRunService : IAutomationRunService
{
    private readonly IAutomationRunRepository _runRepository;
    private readonly IWorkflowHost _workflowHost;
    private readonly ILogger<AutomationRunService> _logger;

    public AutomationRunService(
        IAutomationRunRepository runRepository,
        IWorkflowHost workflowHost,
        ILogger<AutomationRunService> logger)
    {
        _runRepository = runRepository;
        _workflowHost = workflowHost;
        _logger = logger;
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

    public async Task<RunLifecycleResult> SuspendRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _runRepository.GetAsync(runId, cancellationToken);
        if (run is null)
        {
            return RunLifecycleResult.NotFound;
        }

        if (run.Status is not AutomationRunStatus.Running)
        {
            _logger.LogWarning(
                "Cannot suspend run {RunId} in status {Status}", runId, run.Status);
            return RunLifecycleResult.InvalidState;
        }

        if (string.IsNullOrEmpty(run.WorkflowInstanceId))
        {
            return RunLifecycleResult.NoWorkflowInstance;
        }

        await _workflowHost.SuspendWorkflow(run.WorkflowInstanceId);

        run.Status = AutomationRunStatus.Suspended;
        await _runRepository.SaveAsync(run, cancellationToken);

        _logger.LogInformation("Run {RunId} suspended via lifecycle API", runId);
        return RunLifecycleResult.Success;
    }

    public async Task<RunLifecycleResult> ResumeRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _runRepository.GetAsync(runId, cancellationToken);
        if (run is null)
        {
            return RunLifecycleResult.NotFound;
        }

        if (run.Status is not AutomationRunStatus.Suspended)
        {
            _logger.LogWarning(
                "Cannot resume run {RunId} in status {Status}", runId, run.Status);
            return RunLifecycleResult.InvalidState;
        }

        if (string.IsNullOrEmpty(run.WorkflowInstanceId))
        {
            return RunLifecycleResult.NoWorkflowInstance;
        }

        await _workflowHost.ResumeWorkflow(run.WorkflowInstanceId);

        run.Status = AutomationRunStatus.Running;
        await _runRepository.SaveAsync(run, cancellationToken);

        _logger.LogInformation("Run {RunId} resumed via lifecycle API", runId);
        return RunLifecycleResult.Success;
    }

    public async Task<RunLifecycleResult> TerminateRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _runRepository.GetAsync(runId, cancellationToken);
        if (run is null)
        {
            return RunLifecycleResult.NotFound;
        }

        if (run.Status is not (AutomationRunStatus.Running or AutomationRunStatus.Suspended or AutomationRunStatus.Pending))
        {
            _logger.LogWarning(
                "Cannot terminate run {RunId} in status {Status}", runId, run.Status);
            return RunLifecycleResult.InvalidState;
        }

        if (string.IsNullOrEmpty(run.WorkflowInstanceId))
        {
            return RunLifecycleResult.NoWorkflowInstance;
        }

        await _workflowHost.TerminateWorkflow(run.WorkflowInstanceId);

        run.Status = AutomationRunStatus.Cancelled;
        run.CompletedUtc = DateTime.UtcNow;
        run.Error ??= "Workflow terminated by user";
        await _runRepository.SaveAsync(run, cancellationToken);

        _logger.LogInformation("Run {RunId} terminated via lifecycle API", runId);
        return RunLifecycleResult.Success;
    }
}
