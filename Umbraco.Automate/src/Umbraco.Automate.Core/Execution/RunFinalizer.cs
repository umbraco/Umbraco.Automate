using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;
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
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IEventMessagesFactory _eventMessagesFactory;
    private readonly AutomateMetrics _metrics;
    private readonly ILogger<RunFinalizer> _logger;

    public RunFinalizer(
        IAutomationRunRepository runRepository,
        ICoreScopeProvider scopeProvider,
        IEventMessagesFactory eventMessagesFactory,
        AutomateMetrics metrics,
        ILogger<RunFinalizer> logger)
    {
        _runRepository = runRepository;
        _scopeProvider = scopeProvider;
        _eventMessagesFactory = eventMessagesFactory;
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

            // Clean up step runs left in Running status (e.g. from retries that threw
            // before the step status could be updated).
            var now = DateTime.UtcNow;
            foreach (var stepRun in run.StepRuns.Where(sr => sr.Status == StepRunStatus.Running))
            {
                stepRun.Status = StepRunStatus.Failed;
                stepRun.CompletedUtc = now;
                stepRun.Duration = stepRun.CompletedUtc - stepRun.StartedUtc;
                stepRun.Error = "Step was still running when the workflow reached a terminal state";
            }

            var hasFailedStep = run.StepRuns.Any(sr => sr.Status == StepRunStatus.Failed);

            run.Status = workflow.Status == WorkflowStatus.Terminated || hasFailedStep
                ? AutomationRunStatus.Failed
                : AutomationRunStatus.Completed;

            run.CompletedUtc = now;

            if (workflow.Status == WorkflowStatus.Terminated)
            {
                run.Error = "Workflow terminated";
            }

            // Propagate the first step error to the run if no run-level error is set yet.
            if (run.Error is null && hasFailedStep)
            {
                var firstFailed = run.StepRuns.First(sr => sr.Status == StepRunStatus.Failed);
                run.Error = firstFailed.Error;
            }

            await _runRepository.SaveAsync(run, cancellationToken);

            using (ICoreScope scope = _scopeProvider.CreateCoreScope())
            {
                var eventMessages = _eventMessagesFactory.Get();
                scope.Notifications.Publish(new AutomationRunCompletedNotification(run, eventMessages));
                scope.Complete();
            }

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
