using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Core.Sync;
using WorkflowCore.Models;

namespace Umbraco.Automate.Persistence.Runs;

/// <summary>
/// On application startup, marks any automation runs left in <see cref="AutomationRunStatus.Running"/>
/// or <see cref="AutomationRunStatus.Pending"/> as <see cref="AutomationRunStatus.Failed"/>, and
/// terminates their WorkflowCore instances.
/// These represent workflows that were in-flight when the previous process stopped.
/// <para>
/// Terminating the instance is what makes the Failed status true. Left Runnable, the engine resumes
/// it as soon as the host starts and re-executes the interrupted step — repeating any side effect
/// that step already had (an AI call, an HTTP request, an email) behind a run the backoffice shows as
/// failed and will not let a user terminate. For the same reason this runs before the host starts
/// (see <see cref="WorkflowHostLifecycle"/>) rather than on <c>UmbracoApplicationStartedNotification</c>,
/// by which point the engine may already have picked the instance up.
/// </para>
/// Skipped on <see cref="ServerRole.Subscriber"/> nodes — subscribers must not mark runs
/// as failed that may still be executing elsewhere. Runs on all other roles including
/// <see cref="ServerRole.Unknown"/> (role election may not have completed at startup), so the server
/// role alone cannot tell a run this node abandoned from one another node is executing.
/// <para>
/// What can is the workflow lock lease: a node executing an instance holds a lease on its id and keeps
/// renewing it, so a run whose instance has an unexpired lease is still live and is left alone. A
/// lease left behind by a process that just died lapses within
/// <see cref="WorkflowLockOptions.LeaseDuration"/>, so recovery waits at most that long for leases to
/// lapse before failing the runs whose instances are still held.
/// </para>
/// </summary>
internal sealed class EFCoreStuckRunRecovery : IStuckRunRecovery
{
    private const string InterruptedError = "Recovered after application restart — workflow was interrupted";

    private static readonly int[] NonTerminalStatuses =
    [
        (int)AutomationRunStatus.Running,
        (int)AutomationRunStatus.Pending,
    ];

    private static readonly int[] LiveInstanceStatuses =
    [
        (int)WorkflowStatus.Runnable,
        (int)WorkflowStatus.Suspended,
    ];

    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly IOptions<WorkflowLockOptions> _lockOptions;
    private readonly ILogger<EFCoreStuckRunRecovery> _logger;

    public EFCoreStuckRunRecovery(
        IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory,
        IServerRoleAccessor serverRoleAccessor,
        IOptions<WorkflowLockOptions> lockOptions,
        ILogger<EFCoreStuckRunRecovery> logger)
    {
        _dbContextFactory = dbContextFactory;
        _serverRoleAccessor = serverRoleAccessor;
        _lockOptions = lockOptions;
        _logger = logger;
    }

    public async Task RecoverStuckRunsAsync(CancellationToken cancellationToken)
    {
        if (_serverRoleAccessor.CurrentServerRole is ServerRole.Subscriber)
        {
            _logger.LogDebug(
                "Stuck run recovery skipped — this node ({ServerRole}) is a subscriber",
                _serverRoleAccessor.CurrentServerRole);
            return;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;

        var stuckStepStatuses = new[] { (int)StepRunStatus.Pending, (int)StepRunStatus.Running };

        // 1. Recover stuck runs and their step runs.
        var candidateRuns = await ReadCandidateRunsAsync(db, cancellationToken);

        // Leave alone runs another node is still executing (see class remarks).
        var stuckRuns = await ExcludeRunsHeldByLiveLeasesAsync(db, candidateRuns, cancellationToken);

        var stuckRunIds = stuckRuns.Select(r => r.Id).ToList();

        var recoveredSteps = 0;
        var recoveredRuns = 0;
        var terminatedInstances = 0;

        if (stuckRunIds.Count > 0)
        {
            // Terminate the engine instances first: if a later update fails, the worst case is a run
            // that still reads Running over a stopped instance — which the next startup recovers —
            // rather than a Failed run whose instance the engine is about to resume.
            var instanceIds = stuckRuns
                .Where(r => !string.IsNullOrEmpty(r.WorkflowInstanceId))
                .Select(r => r.WorkflowInstanceId!)
                .ToList();

            if (instanceIds.Count > 0)
            {
                // Checked in the statement itself rather than only up front: a node that takes the
                // lease between the check and this update must not have its instance terminated
                // mid-pass — it would persist its in-memory copy as Runnable behind a Failed run.
                var leaseNow = DateTime.UtcNow;

                // Status is a real column for both instance schema versions, and it is what the
                // poller and WorkflowDefinitionRecovery filter on, so updating it is enough to stop
                // the engine. Execution pointers are left as they are, mirroring WorkflowCore's own
                // terminate.
                terminatedInstances = await db.WorkflowInstances
                    .Where(wi => instanceIds.Contains(wi.Id)
                        && LiveInstanceStatuses.Contains(wi.Status)
                        && !db.WorkflowLocks.Any(l => l.LockId == wi.Id && l.ExpiresUtc >= leaseNow))
                    .ExecuteUpdateAsync(
                        s => s
                            .SetProperty(wi => wi.Status, (int)WorkflowStatus.Terminated)
                            .SetProperty(wi => wi.CompleteTime, now)
                            .SetProperty(wi => wi.NextExecution, (long?)null),
                        cancellationToken);
            }

            // The run and step updates re-check status and instance state so they only touch runs that
            // are still in flight and whose instance is no longer live: a run that finished meanwhile,
            // or whose instance was skipped above because a node just leased it, is left alone.
            recoveredSteps = await db.StepRuns
                .Where(sr => stuckRunIds.Contains(sr.RunId)
                    && stuckStepStatuses.Contains(sr.Status)
                    && db.AutomationRuns.Any(r => r.Id == sr.RunId
                        && NonTerminalStatuses.Contains(r.Status)
                        && (r.WorkflowInstanceId == null
                            || !db.WorkflowInstances.Any(wi => wi.Id == r.WorkflowInstanceId
                                && LiveInstanceStatuses.Contains(wi.Status)))))
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(sr => sr.Status, (int)StepRunStatus.Failed)
                        .SetProperty(sr => sr.CompletedUtc, now)
                        .SetProperty(sr => sr.Error, InterruptedError),
                    cancellationToken);

            recoveredRuns = await db.AutomationRuns
                .Where(r => stuckRunIds.Contains(r.Id)
                    && NonTerminalStatuses.Contains(r.Status)
                    && (r.WorkflowInstanceId == null
                        || !db.WorkflowInstances.Any(wi => wi.Id == r.WorkflowInstanceId
                            && LiveInstanceStatuses.Contains(wi.Status))))
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(r => r.Status, (int)AutomationRunStatus.Failed)
                        .SetProperty(r => r.CompletedUtc, now)
                        .SetProperty(r => r.Error, InterruptedError),
                    cancellationToken);
        }

        // 2. Recover orphaned step runs whose parent run already reached a terminal state
        //    but the steps were left in Running/Pending (e.g. from retries that threw before
        //    updating step status).
        var terminalRunStatuses = new[]
        {
            (int)AutomationRunStatus.Completed,
            (int)AutomationRunStatus.Failed,
            (int)AutomationRunStatus.Cancelled,
            (int)AutomationRunStatus.Rejected,
        };

        var orphanedSteps = await db.StepRuns
            .Where(sr => stuckStepStatuses.Contains(sr.Status)
                && db.AutomationRuns.Any(r => r.Id == sr.RunId && terminalRunStatuses.Contains(r.Status)))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(sr => sr.Status, (int)StepRunStatus.Failed)
                    .SetProperty(sr => sr.CompletedUtc, now)
                    .SetProperty(sr => sr.Error, "Recovered after application restart — parent run already completed"),
                cancellationToken);

        recoveredSteps += orphanedSteps;

        if (recoveredRuns > 0 || recoveredSteps > 0)
        {
            _logger.LogWarning(
                "Recovered {RunCount} stuck automation run(s), {StepCount} stuck step run(s) and terminated " +
                "{InstanceCount} workflow instance(s) from previous process",
                recoveredRuns, recoveredSteps, terminatedInstances);
        }
    }

    private static async Task<List<StuckRun>> ReadCandidateRunsAsync(
        UmbracoAutomateDbContext db, CancellationToken cancellationToken)
    {
        // Exclude runs that have steps in a durable status (Sleeping, WaitingForInput) —
        // WorkflowCore will resume these naturally via its persistence mechanism.
        var durableStepStatuses = new[]
        {
            (int)StepRunStatus.Sleeping,
            (int)StepRunStatus.WaitingForInput,
        };

        var durableRunIds = await db.StepRuns
            .Where(sr => durableStepStatuses.Contains(sr.Status))
            .Select(sr => sr.RunId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await db.AutomationRuns
            .Where(r => NonTerminalStatuses.Contains(r.Status) && !durableRunIds.Contains(r.Id))
            .Select(r => new StuckRun(r.Id, r.WorkflowInstanceId))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<StuckRun>> ExcludeRunsHeldByLiveLeasesAsync(
        UmbracoAutomateDbContext db, List<StuckRun> candidates, CancellationToken cancellationToken)
    {
        var instanceIds = GetInstanceIds(candidates);

        if (instanceIds.Count == 0)
        {
            return candidates;
        }

        var leaseDuration = _lockOptions.Value.LeaseDuration;
        var heldInstanceIds = await GetLeasedInstanceIdsAsync(db, instanceIds, cancellationToken);

        if (heldInstanceIds.Count > 0)
        {
            // A lease held by the process that just died lapses on its own; a live node keeps renewing
            // its lease. Give the former time to lapse, bounded by the lease duration, then decide.
            _logger.LogInformation(
                "Waiting {LeaseDuration} for {Count} workflow lease(s) to lapse before recovering stuck runs",
                leaseDuration, heldInstanceIds.Count);

            await Task.Delay(leaseDuration, cancellationToken);

            // Runs finish while we wait, so the candidates read before it are stale: read them again.
            candidates = await ReadCandidateRunsAsync(db, cancellationToken);
            instanceIds = GetInstanceIds(candidates);
            heldInstanceIds = instanceIds.Count == 0
                ? []
                : await GetLeasedInstanceIdsAsync(db, instanceIds, cancellationToken);
        }

        if (heldInstanceIds.Count == 0)
        {
            return candidates;
        }

        _logger.LogInformation(
            "Leaving {Count} run(s) alone — their workflow instances are still leased by another node",
            heldInstanceIds.Count);

        return candidates
            .Where(r => string.IsNullOrEmpty(r.WorkflowInstanceId) || !heldInstanceIds.Contains(r.WorkflowInstanceId))
            .ToList();
    }

    private static List<string> GetInstanceIds(List<StuckRun> candidates) => candidates
        .Where(r => !string.IsNullOrEmpty(r.WorkflowInstanceId))
        .Select(r => r.WorkflowInstanceId!)
        .ToList();

    private static async Task<HashSet<string>> GetLeasedInstanceIdsAsync(
        UmbracoAutomateDbContext db, List<string> instanceIds, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // AsNoTracking + a fresh query each call: the second look must see renewals, not cached rows.
        var held = await db.WorkflowLocks
            .AsNoTracking()
            .Where(l => instanceIds.Contains(l.LockId) && l.ExpiresUtc >= now)
            .Select(l => l.LockId)
            .ToListAsync(cancellationToken);

        return held.ToHashSet();
    }

    private sealed record StuckRun(Guid Id, string? WorkflowInstanceId);
}
