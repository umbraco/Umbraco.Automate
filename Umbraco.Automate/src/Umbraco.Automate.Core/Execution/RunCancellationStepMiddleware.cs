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
/// is <see cref="AutomationRunStatus.Cancelled"/>, skips the step and flips the in-memory
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

        var status = await GetRunStatusAsync(data.RunId, context.CancellationToken);
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

        try
        {
            var status = await _runRepository.GetRunStatusAsync(runId, cancellationToken);
            _cache.Set(cacheKey, status, StatusCacheDuration);
            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read status for run {RunId}; assuming not cancelled", runId);
            return null;
        }
    }
}
