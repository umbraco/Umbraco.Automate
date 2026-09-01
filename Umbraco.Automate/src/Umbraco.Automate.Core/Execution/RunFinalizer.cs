using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Syncs an <see cref="AutomationRun"/>'s status with its WorkflowCore workflow instance.
/// Handles terminal states (<see cref="WorkflowStatus.Complete"/> /
/// <see cref="WorkflowStatus.Terminated"/>) as well as the non-terminal
/// <see cref="WorkflowStatus.Suspended"/> transition used for error-mode Suspend and
/// approval <c>WaitForEvent</c> waits. Called from the persistence provider's
/// <c>PersistWorkflow</c> method.
/// </summary>
internal sealed class RunFinalizer
{
    private readonly IAutomationRunRepository _runRepository;
    private readonly StepOutputHydrationCache _hydrationCache;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IEventMessagesFactory _eventMessagesFactory;
    private readonly AutomateMetrics _metrics;
    private readonly ForEachCollectionCache _collectionCache;
    private readonly ILogger<RunFinalizer> _logger;

    public RunFinalizer(
        IAutomationRunRepository runRepository,
        StepOutputHydrationCache hydrationCache,
        ICoreScopeProvider scopeProvider,
        IEventMessagesFactory eventMessagesFactory,
        AutomateMetrics metrics,
        ForEachCollectionCache collectionCache,
        ILogger<RunFinalizer> logger)
    {
        _runRepository = runRepository;
        _hydrationCache = hydrationCache;
        _scopeProvider = scopeProvider;
        _eventMessagesFactory = eventMessagesFactory;
        _metrics = metrics;
        _collectionCache = collectionCache;
        _logger = logger;
    }

    /// <summary>
    /// Syncs the run status when the workflow reaches Complete, Terminated, or Suspended.
    /// Other statuses (Runnable) are no-ops.
    /// </summary>
    public async Task TryFinalizeAsync(WorkflowInstance workflow, CancellationToken cancellationToken)
    {
        if (workflow.Data is not AutomationWorkflowData data)
        {
            return;
        }

        try
        {
            // Runnable is intentionally not handled here: every step persist hits this path,
            // so we'd pay a DB read per step just to detect a rare Suspended → Running
            // transition. Resume transitions are handled at their explicit call sites
            // (IAutomationRunService.ResumeRunAsync and ActionStepBody.HandleResumeAsync).
            switch (workflow.Status)
            {
                case WorkflowStatus.Complete or WorkflowStatus.Terminated:
                    // Sweep any ForEach/While collections still cached for this run — e.g. a
                    // loop terminated mid-iteration never reaches its own eviction point in
                    // ForEachContainerStepBody. Runs unconditionally, ahead of the early-return
                    // guard below, so it fires even when another path (e.g. user cancellation)
                    // already finalized the run row.
                    _collectionCache.EvictRun(data.RunId);
                    await FinalizeTerminalAsync(workflow, data, cancellationToken);
                    break;

                case WorkflowStatus.Suspended:
                    await SyncSuspendedAsync(data, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync run {RunId} status", data.RunId);
        }
    }

    private async Task FinalizeTerminalAsync(
        WorkflowInstance workflow,
        AutomationWorkflowData data,
        CancellationToken cancellationToken)
    {
        // The run can never bind again — drop any step outputs hydrated for it.
        _hydrationCache.EvictRun(data.RunId);

        var run = await _runRepository.GetAsync(data.RunId, cancellationToken);
        if (run is null || run.Status is AutomationRunStatus.Completed or AutomationRunStatus.Failed
            or AutomationRunStatus.Cancelled or AutomationRunStatus.Rejected)
        {
            return;
        }

        // Clean up step runs left in Running status (e.g. from retries that threw
        // before the step status could be updated). IAutomationRunRepository.SaveAsync
        // does not cascade to StepRuns (see its own doc comment and
        // AutomationRunFactory.UpdateEntity, which only writes the run's own columns), so
        // each mutated step must be persisted explicitly via UpdateStepRunAsync — the same
        // pattern ActionStepBody and AutomationRunService.TerminateRunAsync use — or the
        // status change here is silently dropped.
        var now = DateTime.UtcNow;
        foreach (var stepRun in run.StepRuns.Where(sr => sr.Status == StepRunStatus.Running))
        {
            stepRun.Status = StepRunStatus.Failed;
            stepRun.CompletedUtc = now;
            stepRun.Duration = stepRun.CompletedUtc - stepRun.StartedUtc;
            stepRun.Error = "Step was still running when the workflow reached a terminal state";
            await _runRepository.UpdateStepRunAsync(stepRun, cancellationToken);
        }

        var hasFailedStep = run.StepRuns.Any(sr => sr.Status == StepRunStatus.Failed);
        var hasRejectedStep = run.StepRuns.Any(sr => sr.Status == StepRunStatus.Rejected);

        // Precedence: a real error outranks a refusal. If something broke on the rejected path, the
        // run is Failed — that is the more urgent truth and the one alerting should act on. Only when
        // nothing failed does a refusal surface as Rejected, which is terminal but not an error.
        run.Status = (workflow.Status == WorkflowStatus.Terminated || hasFailedStep, hasRejectedStep) switch
        {
            (true, _) => AutomationRunStatus.Failed,
            (false, true) => AutomationRunStatus.Rejected,
            _ => AutomationRunStatus.Completed,
        };

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

        // A rejected run is not a failed run — counting it as one would make an automation working
        // exactly as designed look unreliable on the dashboard and in the failure metric.
        if (run.Status is AutomationRunStatus.Completed or AutomationRunStatus.Rejected)
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

    private async Task SyncSuspendedAsync(AutomationWorkflowData data, CancellationToken cancellationToken)
    {
        var run = await _runRepository.GetAsync(data.RunId, cancellationToken);

        // Only transition Running → Suspended. Other states (Suspended already, or any
        // terminal state we haven't processed yet) are left alone.
        if (run is null || run.Status is not AutomationRunStatus.Running)
        {
            return;
        }

        run.Status = AutomationRunStatus.Suspended;
        await _runRepository.SaveAsync(run, cancellationToken);

        // The dispatcher handles NotifyOn.Suspended via this notification.
        using (ICoreScope scope = _scopeProvider.CreateCoreScope())
        {
            var eventMessages = _eventMessagesFactory.Get();
            scope.Notifications.Publish(new AutomationRunCompletedNotification(run, eventMessages));
            scope.Complete();
        }

        _logger.LogInformation(
            "Run {RunId} for automation {AutomationAlias} suspended",
            run.Id, data.AutomationAlias);
    }

}
