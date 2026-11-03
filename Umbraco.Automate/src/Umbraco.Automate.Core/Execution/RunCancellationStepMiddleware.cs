using Microsoft.Extensions.Caching.Memory;
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
/// is <see cref="AutomationRunStatus.Cancelled"/> (or any other terminal status), skips the step and flips the in-memory
/// instance to <see cref="WorkflowStatus.Terminated"/> — mirroring WorkflowCore's own
/// <c>TerminateHandler</c>. The executor persists that status at the end of the pass and the
/// consumer skips the workflow from then on. Because the check reads the shared database, it
/// also stops runs cancelled from another node, where an engine-level terminate can be
/// overwritten by the executing node's state snapshot.
/// </para>
/// <para>
/// WorkflowCore's native cooperative-cancel primitive — <c>.CancelCondition(...)</c>, evaluated
/// before every step by its <c>CancellationProcessor</c> — was considered and does not fit here.
/// It compiles the condition against the in-memory <c>workflow.Data</c> snapshot, so it cannot
/// observe a cancel raised out-of-band via the management API or on another node (that updates
/// the durable run row, never this node's in-memory data); and it only cancels the step's
/// execution pointers and descendant scope, never setting <see cref="WorkflowStatus"/>, so the
/// run would not end <see cref="WorkflowStatus.Terminated"/>. This middleware reads the run row
/// (the cross-node source of truth) and sets the terminal status, which the native primitive
/// cannot do. <c>IWorkflowStepMiddleware</c> is itself WorkflowCore's documented per-step
/// extension point, so the reuse stays within the engine's model.
/// </para>
/// <para>
/// It also skips any step whose workflow has already left <see cref="WorkflowStatus.Runnable"/>
/// earlier in the same execution pass. WorkflowCore's executor walks every pointer it collected at
/// the start of a pass without re-checking the workflow status, so once a step has terminated or
/// suspended the workflow, a sibling Parallel branch or parallel ForEach iteration would otherwise
/// still run its step.
/// </para>
/// <para>
/// The status check is cached per run for a short TTL (see <see cref="StatusCacheDuration"/>)
/// because this middleware wraps every step of every run, including every re-entry of
/// ForEach/While/If/Switch containers and every loop iteration — without a cache, a tight loop
/// would issue a DB round-trip per iteration purely to detect a rare cancellation. The check
/// also fails open (treats DB errors as "not cancelled") so a transient DB blip doesn't fail
/// the current step of every active workflow; cancellation is simply detected on a later step.
/// </para>
/// </summary>
internal sealed class RunCancellationStepMiddleware : IWorkflowStepMiddleware
{
    // Balances DB load against how quickly cancellation is noticed: short enough that a
    // cancelled run stops within a fraction of a second, long enough to collapse the many
    // status checks a tight ForEach/While loop would otherwise issue per iteration.
    private static readonly TimeSpan StatusCacheDuration = TimeSpan.FromMilliseconds(250);

    private const string CacheKeyPrefix = "Umbraco.Automate.RunCancellation:";

    private readonly IAutomationRunRepository _runRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RunCancellationStepMiddleware> _logger;

    public RunCancellationStepMiddleware(
        IAutomationRunRepository runRepository,
        IMemoryCache cache,
        ILogger<RunCancellationStepMiddleware> logger)
    {
        _runRepository = runRepository;
        _cache = cache;
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

        // An earlier step in this same execution pass has already ended the workflow — a step
        // failing under Terminate/Suspend, or under Retry when retrying cannot help (see
        // AutomateRetryHandler), via WorkflowCore's error handlers. WorkflowExecutor still walks
        // every pointer it collected at the start of the pass, so without this a sibling Parallel
        // branch or parallel ForEach iteration would run its step after the run was over. Leave
        // the pointer unadvanced, as below, so a Suspended workflow re-runs it on resume.
        if (context.Workflow.Status != WorkflowStatus.Runnable)
        {
            _logger.LogInformation(
                "Workflow {WorkflowInstanceId} is {Status} — skipping step for run {RunId}",
                context.Workflow.Id,
                context.Workflow.Status,
                data.RunId);

            return ExecutionResult.Persist(context.PersistenceData);
        }

        // Any terminal status stops the workflow, not only Cancelled. Only RunFinalizer writes the
        // other terminal statuses in the normal course, once the workflow itself has finished, so
        // seeing one here means the run row and the engine have diverged (e.g. startup recovery
        // failed a run whose instance another node went on to resume). The run row wins: letting
        // the step run would repeat its side effects behind a run the backoffice reports as over.
        var status = await GetRunStatusAsync(data.RunId, context.CancellationToken);
        if (status is not (AutomationRunStatus.Cancelled or AutomationRunStatus.Failed
            or AutomationRunStatus.Completed or AutomationRunStatus.Rejected))
        {
            return await next();
        }

        _logger.LogInformation(
            "Run {RunId} is {Status} — skipping step and terminating workflow {WorkflowInstanceId}",
            data.RunId,
            status,
            context.Workflow.Id);

        // Two benign timing quirks, both bounded and safe to leave as-is:
        // - A cancel landing in the sub-ms window while a DelayAction tick is being processed
        //   defers the stop until the delay elapses (the step returns SleepFor before this
        //   check runs again). A cancel during the sleep itself — the common case — hits a
        //   free lock and stops immediately.
        // - The run row is stamped Cancelled/CompletedUtc up front by TerminateRunAsync, so up
        //   to one cache TTL of further iterations may append Completed StepRuns timestamped
        //   after the run's CompletedUtc. A harmless ordering artefact, not lost work.

        context.Workflow.Status = WorkflowStatus.Terminated;
        context.Workflow.CompleteTime = DateTime.UtcNow;

        // Keep the pointer active-but-unadvanced (like an engine-level terminate, which
        // leaves pointers dangling); advancing it could complete the final pointer and
        // let the executor overwrite Terminated with Complete.
        return ExecutionResult.Persist(context.PersistenceData);
    }

    /// <summary>
    /// Gets the run status, served from a short-TTL cache when available. Fails open — any
    /// exception from the DB read is logged and treated as "not cancelled" so a transient
    /// failure doesn't fail the step currently executing.
    /// </summary>
    private async Task<AutomationRunStatus?> GetRunStatusAsync(Guid runId, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeyPrefix + runId;
        if (_cache.TryGetValue(cacheKey, out AutomationRunStatus? cachedStatus))
        {
            return cachedStatus;
        }

        AutomationRunStatus? status;
        try
        {
            status = await _runRepository.GetRunStatusAsync(runId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read status for run {RunId}; assuming not cancelled", runId);
            return null;
        }

        // Cache the result outside the read's try/catch: a Set failure (e.g. a size-limited
        // cache rejecting an entry with no Size) must never discard an already-read status,
        // or a Cancelled read would be swallowed and cancellation would silently stop working.
        try
        {
            _cache.Set(cacheKey, status, StatusCacheDuration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache status for run {RunId}; continuing uncached", runId);
        }

        return status;
    }
}
