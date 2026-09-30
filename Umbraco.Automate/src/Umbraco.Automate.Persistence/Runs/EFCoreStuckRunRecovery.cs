using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Persistence.Workflows;
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
/// What can is the node heartbeat (see <see cref="IWorkflowNodeHeartbeatStore"/>). A workflow lock is
/// only held while a node is inside an execution pass, so a run that is queued, between steps or
/// waiting to retry holds none — but any node consuming workflow work will run its next pass, and such
/// a node keeps heartbeating. So if any other node is live, no in-flight run is stuck and recovery
/// leaves them all to it; only when no other node is live does it fail them. A node counts as live
/// when its beat counter changes within <see cref="WorkflowLockOptions.LeaseDuration"/>, which needs no
/// comparison between clocks; recovery stops waiting as soon as it sees a beat, or once every other
/// node's row has gone.
/// </para>
/// <para>
/// A node that joins while recovery is failing runs is still protected: an instance is only terminated
/// when no lease on it is held, other than a lease owned by a node recovery has just seen stop.
/// </para>
/// </summary>
internal sealed class EFCoreStuckRunRecovery : IStuckRunRecovery
{
    private const string InterruptedError = "Recovered after application restart — workflow was interrupted";

    // A heartbeat this far behind the recovering node's clock is from a node long gone, not one with a
    // skewed clock, so it is discarded without waiting for it to change.
    private const int StaleHeartbeatLeaseMultiple = 10;

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
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EFCoreStuckRunRecovery> _logger;

    public EFCoreStuckRunRecovery(
        IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory,
        IServerRoleAccessor serverRoleAccessor,
        IOptions<WorkflowLockOptions> lockOptions,
        TimeProvider timeProvider,
        ILogger<EFCoreStuckRunRecovery> logger)
    {
        _dbContextFactory = dbContextFactory;
        _serverRoleAccessor = serverRoleAccessor;
        _lockOptions = lockOptions;
        _timeProvider = timeProvider;
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

        var stuckStepStatuses = new[] { (int)StepRunStatus.Pending, (int)StepRunStatus.Running };

        // 1. Recover stuck runs and their step runs — but only when no other node is live to carry
        //    them on (see class remarks).
        var stuckRuns = await ReadCandidateRunsAsync(db, cancellationToken);
        var stoppedNodeIds = new List<Guid>();

        if (stuckRuns.Count > 0)
        {
            var liveness = await WatchOtherNodesAsync(db, cancellationToken);
            stoppedNodeIds = liveness.StoppedNodeIds;

            if (liveness.AnyLive)
            {
                _logger.LogInformation(
                    "Leaving {Count} in-flight run(s) alone — another node is live and will continue them",
                    stuckRuns.Count);
                stuckRuns = [];
            }
            else if (liveness.Waited)
            {
                // Runs finish while we wait, so the candidates read before it are stale: read them again.
                stuckRuns = await ReadCandidateRunsAsync(db, cancellationToken);
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
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
                // Checked in the statement itself: a node that joined after the liveness check and
                // took the lease must not have its instance terminated mid-pass — it would persist its
                // in-memory copy as Runnable behind a Failed run. Leases owned by a node just seen to
                // stop are not held by anyone, so they don't count.
                var leaseNow = _timeProvider.GetUtcNow().UtcDateTime;

                // Status is a real column for both instance schema versions, and it is what the
                // poller and WorkflowDefinitionRecovery filter on, so updating it is enough to stop
                // the engine. Execution pointers are left as they are, mirroring WorkflowCore's own
                // terminate.
                terminatedInstances = await db.WorkflowInstances
                    .Where(wi => instanceIds.Contains(wi.Id)
                        && LiveInstanceStatuses.Contains(wi.Status)
                        && !db.WorkflowLocks.Any(l => l.LockId == wi.Id
                            && l.ExpiresUtc >= leaseNow
                            && !stoppedNodeIds.Contains(l.OwnerToken)))
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

        if (stoppedNodeIds.Count > 0)
        {
            // Those nodes are gone; drop their rows so the next startup does not wait on them.
            await db.WorkflowNodeHeartbeats
                .Where(h => stoppedNodeIds.Contains(h.NodeId))
                .ExecuteDeleteAsync(cancellationToken);
        }

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

    /// <summary>
    /// Decides whether any other node is live by watching the heartbeat rows' beat counters for up to
    /// <see cref="WorkflowLockOptions.LeaseDuration"/>. Returns as soon as one changes (or a new node
    /// appears), or as soon as no other node is left to watch.
    /// </summary>
    private async Task<NodeLiveness> WatchOtherNodesAsync(
        UmbracoAutomateDbContext db, CancellationToken cancellationToken)
    {
        var options = _lockOptions.Value;
        var started = _timeProvider.GetUtcNow();
        var staleBefore = started.UtcDateTime - options.LeaseDuration * StaleHeartbeatLeaseMultiple;

        var stopped = new List<Guid>();
        var watched = new Dictionary<Guid, long>();

        foreach (var heartbeat in await ReadHeartbeatsAsync(db, cancellationToken))
        {
            if (heartbeat.HeartbeatUtc < staleBefore)
            {
                stopped.Add(heartbeat.NodeId);
            }
            else
            {
                watched[heartbeat.NodeId] = heartbeat.Beat;
            }
        }

        if (watched.Count == 0)
        {
            return new NodeLiveness(false, stopped, Waited: false);
        }

        _logger.LogInformation(
            "Waiting up to {LeaseDuration} for a heartbeat from {Count} other node(s) before recovering stuck runs",
            options.LeaseDuration, watched.Count);

        var deadline = started + options.LeaseDuration;
        var pollInterval = options.RenewalInterval < TimeSpan.FromSeconds(1)
            ? options.RenewalInterval
            : TimeSpan.FromSeconds(1);

        while (true)
        {
            var remaining = deadline - _timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(remaining < pollInterval ? remaining : pollInterval, _timeProvider, cancellationToken);

            var current = await ReadHeartbeatsAsync(db, cancellationToken);

            foreach (var heartbeat in current)
            {
                var isNew = !watched.TryGetValue(heartbeat.NodeId, out var beat) && !stopped.Contains(heartbeat.NodeId);
                if (isNew || (watched.ContainsKey(heartbeat.NodeId) && beat != heartbeat.Beat))
                {
                    return new NodeLiveness(true, stopped, Waited: true);
                }
            }

            // A node that shut down cleanly removed its row: nothing left to wait for from it.
            var present = current.Select(h => h.NodeId).ToHashSet();
            foreach (var nodeId in watched.Keys.Where(id => !present.Contains(id)).ToList())
            {
                watched.Remove(nodeId);
            }

            if (watched.Count == 0)
            {
                return new NodeLiveness(false, stopped, Waited: true);
            }
        }

        stopped.AddRange(watched.Keys);
        return new NodeLiveness(false, stopped, Waited: true);
    }

    private static Task<List<WorkflowNodeHeartbeatEntity>> ReadHeartbeatsAsync(
        UmbracoAutomateDbContext db, CancellationToken cancellationToken) =>
        // AsNoTracking + a fresh query each call: every look must see new beats, not cached rows.
        db.WorkflowNodeHeartbeats.AsNoTracking().ToListAsync(cancellationToken);

    private sealed record NodeLiveness(bool AnyLive, List<Guid> StoppedNodeIds, bool Waited);

    private sealed record StuckRun(Guid Id, string? WorkflowInstanceId);
}
