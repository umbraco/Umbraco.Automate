using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Runs;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Cooperatively stops workflow execution for runs that have been cancelled.
/// <para>
/// <c>IWorkflowHost.TerminateWorkflow</c> makes a single attempt to acquire the per-workflow
/// lock, which the executor holds for the duration of every execution pass — so a terminate
/// issued while a run is actively executing (the common case when a user cancels a long loop)
/// silently fails and the workflow runs to completion. The run row is the durable source of
/// truth for cancellation, so this middleware checks it before every step and, when the run
/// is <see cref="AutomationRunStatus.Cancelled"/>, skips the step and flips the in-memory
/// instance to <see cref="WorkflowStatus.Terminated"/> — mirroring WorkflowCore's own
/// <c>TerminateHandler</c>. The executor persists that status at the end of the pass and the
/// consumer skips the workflow from then on. Because the check reads the shared database, it
/// also stops runs cancelled from another node, where an engine-level terminate can be
/// overwritten by the executing node's state snapshot.
/// </para>
/// </summary>
internal sealed class RunCancellationStepMiddleware : IWorkflowStepMiddleware
{
    private readonly IAutomationRunRepository _runRepository;
    private readonly ILogger<RunCancellationStepMiddleware> _logger;

    public RunCancellationStepMiddleware(
        IAutomationRunRepository runRepository,
        ILogger<RunCancellationStepMiddleware> logger)
    {
        _runRepository = runRepository;
        _logger = logger;
    }

    public async Task<ExecutionResult> HandleAsync(
        IStepExecutionContext context,
        IStepBody body,
        WorkflowStepDelegate next)
    {
        if (context.Workflow.Data is not AutomationWorkflowData data)
        {
            return await next();
        }

        var status = await _runRepository.GetRunStatusAsync(data.RunId, context.CancellationToken);
        if (status is not AutomationRunStatus.Cancelled)
        {
            return await next();
        }

        _logger.LogInformation(
            "Run {RunId} is cancelled — skipping step and terminating workflow {WorkflowInstanceId}",
            data.RunId,
            context.Workflow.Id);

        context.Workflow.Status = WorkflowStatus.Terminated;
        context.Workflow.CompleteTime = DateTime.UtcNow;

        // Keep the pointer active-but-unadvanced (like an engine-level terminate, which
        // leaves pointers dangling); advancing it could complete the final pointer and
        // let the executor overwrite Terminated with Complete.
        return ExecutionResult.Persist(context.PersistenceData);
    }
}
