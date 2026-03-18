using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Runs;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Finalizes an <see cref="AutomationRun"/> when its WorkflowCore workflow instance
/// reaches a terminal state (<see cref="WorkflowStatus.Complete"/> or <see cref="WorkflowStatus.Terminated"/>).
/// Called from the persistence provider's <c>PersistWorkflow</c> method.
/// </summary>
internal sealed class RunFinalizer
{
    private readonly IAutomationRunRepository _runRepository;
    private readonly AutomateMetrics _metrics;
    private readonly ILogger<RunFinalizer> _logger;

    public RunFinalizer(
        IAutomationRunRepository runRepository,
        AutomateMetrics metrics,
        ILogger<RunFinalizer> logger)
    {
        _runRepository = runRepository;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// Checks whether the workflow has reached a terminal state and, if so,
    /// updates the corresponding <see cref="AutomationRun"/> status.
    /// </summary>
    public async Task TryFinalizeAsync(WorkflowInstance workflow, CancellationToken cancellationToken)
    {
        if (workflow.Status is not (WorkflowStatus.Complete or WorkflowStatus.Terminated))
        {
            return;
        }

        if (workflow.Data is not AutomationWorkflowData data)
        {
            return;
        }

        try
        {
            var run = await _runRepository.GetAsync(data.RunId, cancellationToken);
            if (run is null || run.Status is not AutomationRunStatus.Running)
            {
                return;
            }

            var hasFailedStep = run.StepRuns.Any(sr => sr.Status == StepRunStatus.Failed);

            run.Status = workflow.Status == WorkflowStatus.Terminated || hasFailedStep
                ? AutomationRunStatus.Failed
                : AutomationRunStatus.Completed;

            run.CompletedUtc = DateTime.UtcNow;

            if (workflow.Status == WorkflowStatus.Terminated)
            {
                run.Error = "Workflow terminated";
            }

            await _runRepository.SaveAsync(run, cancellationToken);

            if (run.Status == AutomationRunStatus.Completed)
            {
                _metrics.RunCompleted(data.AutomationAlias);
            }
            else
            {
                _metrics.RunFailed(data.AutomationAlias);
            }

            if (run.StartedUtc.HasValue && run.CompletedUtc.HasValue)
            {
                var durationMs = (run.CompletedUtc.Value - run.StartedUtc.Value).TotalMilliseconds;
                _metrics.RecordRunDuration(durationMs, data.AutomationAlias);
            }

            _logger.LogInformation(
                "Run {RunId} for automation {AutomationAlias} finalized as {Status}",
                run.Id, data.AutomationAlias, run.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to finalize run {RunId}", data.RunId);
        }
    }
}
