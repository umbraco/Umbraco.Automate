using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Persistence.Workflows;
using Umbraco.Automate.Tests.Common.Fixtures;
using Umbraco.Cms.Core.Sync;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="EFCoreStuckRunRecovery"/> against in-memory SQLite. The reported
/// bug: recovery failed the run row but left its WorkflowCore instance Runnable, so the engine resumed
/// it on startup and re-ran the interrupted AI step — spending credits behind a run the backoffice
/// showed as failed and would not let anyone terminate.
/// <para>
/// Also covers #423: recovery must leave alone runs another live node will carry on with, even when
/// they hold no workflow lock (queued, between steps, waiting to retry) — decided by node heartbeats.
/// </para>
/// </summary>
public class StuckRunRecoveryTests : IDisposable
{
    private readonly EfCoreTestFixture _fixture = new();
    private readonly Mock<IServerRoleAccessor> _serverRoleAccessor = new();
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan RenewalInterval = TimeSpan.FromMilliseconds(50);

    private readonly EFCoreStuckRunRecovery _recovery;

    public StuckRunRecoveryTests()
    {
        _serverRoleAccessor.Setup(r => r.CurrentServerRole).Returns(ServerRole.Single);

        _recovery = new EFCoreStuckRunRecovery(
            new TestDbContextFactory(_fixture.CreateContext),
            _serverRoleAccessor.Object,
            Options.Create(new WorkflowLockOptions { LeaseDuration = LeaseDuration, RenewalInterval = RenewalInterval }),
            TimeProvider.System,
            NullLogger<EFCoreStuckRunRecovery>.Instance);
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_InterruptedRun_FailsRunAndTerminatesItsInstance()
    {
        var (runId, instanceId) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Running, WorkflowStatus.Runnable);

        await _recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        var run = await db.AutomationRuns.SingleAsync(r => r.Id == runId);
        run.Status.ShouldBe((int)AutomationRunStatus.Failed);
        run.Error.ShouldNotBeNull();

        var step = await db.StepRuns.SingleAsync(sr => sr.RunId == runId);
        step.Status.ShouldBe((int)StepRunStatus.Failed);

        var instance = await db.WorkflowInstances.SingleAsync(wi => wi.Id == instanceId);
        instance.Status.ShouldBe((int)WorkflowStatus.Terminated);
        instance.CompleteTime.ShouldNotBeNull();
        instance.NextExecution.ShouldBeNull();
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_InterruptedRun_LeavesItsCompletedStepsAlone()
    {
        var (runId, _) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Running, WorkflowStatus.Runnable);
        var completedStepId = await AddStepRunAsync(runId, StepRunStatus.Completed);

        await _recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        var completed = await db.StepRuns.SingleAsync(sr => sr.Id == completedStepId);
        completed.Status.ShouldBe((int)StepRunStatus.Completed);
        completed.Error.ShouldBeNull();
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_PendingRun_FailsRunAndItsSteps()
    {
        var (runId, _) = await SeedRunAsync(
            AutomationRunStatus.Pending, StepRunStatus.Pending, WorkflowStatus.Runnable);

        await _recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe((int)AutomationRunStatus.Failed);
        (await db.StepRuns.SingleAsync(sr => sr.RunId == runId)).Status.ShouldBe((int)StepRunStatus.Failed);
    }

    [Theory]
    [InlineData(StepRunStatus.Sleeping)]
    [InlineData(StepRunStatus.WaitingForInput)]
    public async Task RecoverStuckRunsAsync_DurableRun_LeavesRunAndInstanceToResume(StepRunStatus durableStatus)
    {
        var (runId, instanceId) = await SeedRunAsync(
            AutomationRunStatus.Running, durableStatus, WorkflowStatus.Runnable);

        await _recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe((int)AutomationRunStatus.Running);
        (await db.WorkflowInstances.SingleAsync(wi => wi.Id == instanceId)).Status.ShouldBe((int)WorkflowStatus.Runnable);
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_ApprovalDecidedButNotYetRouted_FailsTheRun()
    {
        // Pins a known gap (#467): the decision was saved on the approval step run (Completed) and the
        // run moved back to Running, but the process stopped before the workflow was persisted. No step
        // is waiting, so recovery fails the run instead of letting the step route by the decision.
        var (runId, _) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Completed, WorkflowStatus.Runnable);

        await _recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe((int)AutomationRunStatus.Failed);
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_FinishedRun_DoesNotTouchItsInstance()
    {
        var (_, instanceId) = await SeedRunAsync(
            AutomationRunStatus.Completed, StepRunStatus.Completed, WorkflowStatus.Complete);

        await _recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        (await db.WorkflowInstances.SingleAsync(wi => wi.Id == instanceId)).Status.ShouldBe((int)WorkflowStatus.Complete);
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_CompletedRunWithOrphanedStep_FailsTheStepOnly()
    {
        var (runId, _) = await SeedRunAsync(
            AutomationRunStatus.Completed, StepRunStatus.Running, WorkflowStatus.Complete);

        await _recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        var run = await db.AutomationRuns.SingleAsync(r => r.Id == runId);
        run.Status.ShouldBe((int)AutomationRunStatus.Completed);
        run.Error.ShouldBeNull();

        var step = await db.StepRuns.SingleAsync(sr => sr.RunId == runId);
        step.Status.ShouldBe((int)StepRunStatus.Failed);
        step.Error.ShouldBe("Recovered after application restart — parent run already completed");
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_SubscriberNode_LeavesEverythingAlone()
    {
        _serverRoleAccessor.Setup(r => r.CurrentServerRole).Returns(ServerRole.Subscriber);
        var (runId, instanceId) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Running, WorkflowStatus.Runnable);

        await _recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe((int)AutomationRunStatus.Running);
        (await db.WorkflowInstances.SingleAsync(wi => wi.Id == instanceId)).Status.ShouldBe((int)WorkflowStatus.Runnable);
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_InstanceLeasedByAnotherNode_LeavesRunAndInstanceAlone()
    {
        var (runId, instanceId) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Running, WorkflowStatus.Runnable);
        await SeedLeaseAsync(instanceId, DateTime.UtcNow.AddHours(1));

        await _recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe((int)AutomationRunStatus.Running);
        (await db.StepRuns.SingleAsync(sr => sr.RunId == runId)).Status.ShouldBe((int)StepRunStatus.Running);
        (await db.WorkflowInstances.SingleAsync(wi => wi.Id == instanceId)).Status.ShouldBe((int)WorkflowStatus.Runnable);
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_UnexpiredLeaseLeftByStoppedNode_RecoversRunAndPrunesTheNode()
    {
        var (runId, instanceId) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Running, WorkflowStatus.Runnable);
        var deadNode = Guid.NewGuid();
        await SeedHeartbeatAsync(deadNode, DateTime.UtcNow);
        await SeedLeaseAsync(instanceId, DateTime.UtcNow.AddHours(1), deadNode);

        await _recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe((int)AutomationRunStatus.Failed);
        (await db.WorkflowInstances.SingleAsync(wi => wi.Id == instanceId)).Status.ShouldBe((int)WorkflowStatus.Terminated);
        (await db.WorkflowNodeHeartbeats.AnyAsync(h => h.NodeId == deadNode)).ShouldBeFalse();
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_LiveNodeHeartbeating_LeavesUnleasedRunsAlone()
    {
        // A generous wait: the live node's beat ends it early, so this only bounds a slow CI agent.
        var recovery = CreateRecovery(TimeSpan.FromSeconds(10), RenewalInterval);
        // Between steps / waiting to retry: no lease on the instance, but its node is alive.
        var (betweenStepsRunId, betweenStepsInstanceId) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Pending, WorkflowStatus.Runnable);
        // Queued in the outbox: no workflow instance yet.
        var queuedRunId = await SeedRunWithoutInstanceAsync();

        using var liveNode = StartHeartbeating(Guid.NewGuid());
        await liveNode.FirstBeat;

        await recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == betweenStepsRunId)).Status.ShouldBe((int)AutomationRunStatus.Running);
        (await db.StepRuns.SingleAsync(sr => sr.RunId == betweenStepsRunId)).Status.ShouldBe((int)StepRunStatus.Pending);
        (await db.WorkflowInstances.SingleAsync(wi => wi.Id == betweenStepsInstanceId)).Status.ShouldBe((int)WorkflowStatus.Runnable);
        (await db.AutomationRuns.SingleAsync(r => r.Id == queuedRunId)).Status.ShouldBe((int)AutomationRunStatus.Pending);
    }

    [Theory]
    [InlineData(WorkflowStatus.Terminated)]
    [InlineData(WorkflowStatus.Complete)]
    public async Task RecoverStuckRunsAsync_LiveNodeHeartbeating_StillFailsRunsWhoseInstanceHasEnded(WorkflowStatus endedStatus)
    {
        var recovery = CreateRecovery(TimeSpan.FromSeconds(10), RenewalInterval);
        // The instance ended long ago but the run's own update was lost in a crash: no node will move it on.
        var (endedRunId, endedInstanceId) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Running, endedStatus, DateTime.UtcNow.AddHours(-1));
        // Between steps: the live node carries this one on.
        var (liveRunId, liveInstanceId) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Pending, WorkflowStatus.Runnable);

        using var liveNode = StartHeartbeating(Guid.NewGuid());
        await liveNode.FirstBeat;

        await recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        var endedRun = await db.AutomationRuns.SingleAsync(r => r.Id == endedRunId);
        endedRun.Status.ShouldBe((int)AutomationRunStatus.Failed);
        endedRun.Error.ShouldNotBeNull();
        (await db.StepRuns.SingleAsync(sr => sr.RunId == endedRunId)).Status.ShouldBe((int)StepRunStatus.Failed);
        (await db.WorkflowInstances.SingleAsync(wi => wi.Id == endedInstanceId)).Status.ShouldBe((int)endedStatus);
        (await db.AutomationRuns.SingleAsync(r => r.Id == liveRunId)).Status.ShouldBe((int)AutomationRunStatus.Running);
        (await db.WorkflowInstances.SingleAsync(wi => wi.Id == liveInstanceId)).Status.ShouldBe((int)WorkflowStatus.Runnable);
    }

    [Theory]
    [InlineData(WorkflowStatus.Terminated)]
    [InlineData(WorkflowStatus.Complete)]
    public async Task RecoverStuckRunsAsync_LiveNodeHeartbeating_LeavesARunWhoseInstanceHasJustEnded(WorkflowStatus endedStatus)
    {
        var recovery = CreateRecovery(TimeSpan.FromSeconds(10), RenewalInterval);
        // The live node has saved the finished instance but not yet finalised the run.
        var (runId, _) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Completed, endedStatus, DateTime.UtcNow);

        using var liveNode = StartHeartbeating(Guid.NewGuid());
        await liveNode.FirstBeat;

        await recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe((int)AutomationRunStatus.Running);
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_LiveNodeHeartbeating_StopsWaitingAtItsFirstBeat()
    {
        var recovery = CreateRecovery(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(50));
        var (runId, _) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Running, WorkflowStatus.Runnable);

        using var liveNode = StartHeartbeating(Guid.NewGuid());
        await liveNode.FirstBeat;

        var elapsed = await TimeAsync(() => recovery.RecoverStuckRunsAsync(CancellationToken.None));

        elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe((int)AutomationRunStatus.Running);
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_NoOtherNodes_RecoversWithoutWaiting()
    {
        var recovery = CreateRecovery(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10));
        var (runId, _) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Running, WorkflowStatus.Runnable);

        var elapsed = await TimeAsync(() => recovery.RecoverStuckRunsAsync(CancellationToken.None));

        elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe((int)AutomationRunStatus.Failed);
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_LongStaleHeartbeat_RecoversWithoutWaitingAndPrunesIt()
    {
        var recovery = CreateRecovery(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10));
        var (runId, _) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Running, WorkflowStatus.Runnable);
        var goneNode = Guid.NewGuid();
        await SeedHeartbeatAsync(goneNode, DateTime.UtcNow.AddHours(-1));

        var elapsed = await TimeAsync(() => recovery.RecoverStuckRunsAsync(CancellationToken.None));

        elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe((int)AutomationRunStatus.Failed);
        (await db.WorkflowNodeHeartbeats.AnyAsync(h => h.NodeId == goneNode)).ShouldBeFalse();
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_OtherNodeShutsDownDuringWait_RecoversOnceItsRowIsGone()
    {
        var recovery = CreateRecovery(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(50));
        var (runId, _) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Running, WorkflowStatus.Runnable);
        var leavingNode = Guid.NewGuid();
        await SeedHeartbeatAsync(leavingNode, DateTime.UtcNow);

        var recovering = TimeAsync(() => recovery.RecoverStuckRunsAsync(CancellationToken.None));

        await Task.Delay(100);
        await using (var other = _fixture.CreateContext())
        {
            await other.WorkflowNodeHeartbeats.Where(h => h.NodeId == leavingNode).ExecuteDeleteAsync();
        }

        var elapsed = await recovering;

        elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe((int)AutomationRunStatus.Failed);
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_ReleasedLease_RecoversRun()
    {
        var (runId, instanceId) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Running, WorkflowStatus.Runnable);
        await SeedLeaseAsync(instanceId, DateTime.MinValue);

        await _recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe((int)AutomationRunStatus.Failed);
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_RunCompletesDuringHeartbeatWait_LeavesItCompleted()
    {
        var (runId, instanceId) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Running, WorkflowStatus.Runnable);
        var stoppingNode = Guid.NewGuid();
        await SeedHeartbeatAsync(stoppingNode, DateTime.UtcNow);
        await SeedLeaseAsync(instanceId, DateTime.UtcNow.AddHours(1), stoppingNode);

        // Long enough that the run finishes well inside the wait, even on a slow CI agent.
        var recovery = CreateRecovery(TimeSpan.FromSeconds(3), RenewalInterval)
            .RecoverStuckRunsAsync(CancellationToken.None);

        // The node finishes the run and releases its lease while recovery is waiting, then stops.
        await Task.Delay(100);
        await using (var other = _fixture.CreateContext())
        {
            await other.StepRuns.Where(sr => sr.RunId == runId)
                .ExecuteUpdateAsync(s => s.SetProperty(sr => sr.Status, (int)StepRunStatus.Completed));
            await other.AutomationRuns.Where(r => r.Id == runId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, (int)AutomationRunStatus.Completed));
            await other.WorkflowInstances.Where(wi => wi.Id == instanceId)
                .ExecuteUpdateAsync(s => s.SetProperty(wi => wi.Status, (int)WorkflowStatus.Complete));
            await other.WorkflowLocks.Where(l => l.LockId == instanceId).ExecuteDeleteAsync();
        }

        await recovery;

        await using var db = _fixture.CreateContext();
        var run = await db.AutomationRuns.SingleAsync(r => r.Id == runId);
        run.Status.ShouldBe((int)AutomationRunStatus.Completed);
        run.Error.ShouldBeNull();
        (await db.StepRuns.SingleAsync(sr => sr.RunId == runId)).Status.ShouldBe((int)StepRunStatus.Completed);
        (await db.WorkflowInstances.SingleAsync(wi => wi.Id == instanceId)).Status.ShouldBe((int)WorkflowStatus.Complete);
    }

    [Fact]
    public async Task RecoverStuckRunsAsync_MixOfLeasedAndUnleasedRuns_RecoversOnlyTheUnleased()
    {
        var (leasedRunId, leasedInstanceId) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Running, WorkflowStatus.Runnable);
        await SeedLeaseAsync(leasedInstanceId, DateTime.UtcNow.AddHours(1));
        var (pendingRunId, _) = await SeedRunAsync(
            AutomationRunStatus.Pending, StepRunStatus.Pending, WorkflowStatus.Runnable);

        await _recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == leasedRunId)).Status.ShouldBe((int)AutomationRunStatus.Running);
        (await db.AutomationRuns.SingleAsync(r => r.Id == pendingRunId)).Status.ShouldBe((int)AutomationRunStatus.Failed);
    }

    private EFCoreStuckRunRecovery CreateRecovery(TimeSpan leaseDuration, TimeSpan renewalInterval) => new(
        new TestDbContextFactory(_fixture.CreateContext),
        _serverRoleAccessor.Object,
        Options.Create(new WorkflowLockOptions { LeaseDuration = leaseDuration, RenewalInterval = renewalInterval }),
        TimeProvider.System,
        NullLogger<EFCoreStuckRunRecovery>.Instance);

    private static async Task<TimeSpan> TimeAsync(Func<Task> action)
    {
        var started = DateTime.UtcNow;
        await action();
        return DateTime.UtcNow - started;
    }

    private async Task SeedHeartbeatAsync(Guid nodeId, DateTime heartbeatUtc)
    {
        await using var db = _fixture.CreateContext();
        db.WorkflowNodeHeartbeats.Add(new WorkflowNodeHeartbeatEntity
        {
            NodeId = nodeId,
            Beat = 1,
            HeartbeatUtc = heartbeatUtc,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Simulates another live node by writing its heartbeat through the real store.</summary>
    private HeartbeatingNode StartHeartbeating(Guid nodeId)
    {
        var store = new EFCoreWorkflowNodeHeartbeatStore(new TestDbContextFactory(_fixture.CreateContext));
        return new HeartbeatingNode(store, nodeId, RenewalInterval);
    }

    private sealed class HeartbeatingNode : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly TaskCompletionSource _firstBeat = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task _loop;

        public HeartbeatingNode(EFCoreWorkflowNodeHeartbeatStore store, Guid nodeId, TimeSpan interval)
        {
            _loop = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    await store.BeatAsync(nodeId, DateTime.UtcNow, CancellationToken.None);
                    _firstBeat.TrySetResult();
                    try
                    {
                        await Task.Delay(interval, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            });
        }

        public Task FirstBeat => _firstBeat.Task;

        public void Dispose()
        {
            _cts.Cancel();
            _loop.GetAwaiter().GetResult();
            _cts.Dispose();
        }
    }

    private async Task SeedLeaseAsync(string lockId, DateTime expiresUtc, Guid? ownerToken = null)
    {
        await using var db = _fixture.CreateContext();
        db.WorkflowLocks.Add(new WorkflowLockEntity
        {
            LockId = lockId,
            OwnerToken = ownerToken ?? Guid.NewGuid(),
            AcquiredUtc = DateTime.UtcNow,
            ExpiresUtc = expiresUtc,
        });
        await db.SaveChangesAsync();
    }

    private async Task<(Guid RunId, string InstanceId)> SeedRunAsync(
        AutomationRunStatus runStatus,
        StepRunStatus stepStatus,
        WorkflowStatus instanceStatus,
        DateTime? instanceCompleteTime = null)
    {
        var runId = Guid.NewGuid();
        var instanceId = Guid.NewGuid().ToString();

        await using var db = _fixture.CreateContext();

        db.WorkflowInstances.Add(new WorkflowInstanceEntity
        {
            Id = instanceId,
            WorkflowDefinitionId = $"automate-{Guid.NewGuid()}-v1",
            Version = 1,
            Status = (int)instanceStatus,
            CreateTime = DateTime.UtcNow,
            CompleteTime = instanceCompleteTime,
            NextExecution = 0,
            SchemaVersion = 1,
            Data = "{}",
        });

        db.AutomationRuns.Add(new AutomationRunEntity
        {
            Id = runId,
            AutomationId = Guid.NewGuid(),
            AutomationVersion = 1,
            Status = (int)runStatus,
            StartedUtc = DateTime.UtcNow,
            InitiatedBy = "system",
            WorkflowInstanceId = instanceId,
        });

        db.StepRuns.Add(new StepRunEntity
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            StepId = Guid.NewGuid(),
            ActionAlias = "runAIAgent",
            Status = (int)stepStatus,
            StartedUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        return (runId, instanceId);
    }

    private async Task<Guid> SeedRunWithoutInstanceAsync()
    {
        var runId = Guid.NewGuid();

        await using var db = _fixture.CreateContext();
        db.AutomationRuns.Add(new AutomationRunEntity
        {
            Id = runId,
            AutomationId = Guid.NewGuid(),
            AutomationVersion = 1,
            Status = (int)AutomationRunStatus.Pending,
            StartedUtc = DateTime.UtcNow,
            InitiatedBy = "system",
        });

        await db.SaveChangesAsync();
        return runId;
    }

    private async Task<Guid> AddStepRunAsync(Guid runId, StepRunStatus status)
    {
        var stepRunId = Guid.NewGuid();

        await using var db = _fixture.CreateContext();
        db.StepRuns.Add(new StepRunEntity
        {
            Id = stepRunId,
            RunId = runId,
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.logMessage",
            Status = (int)status,
            StartedUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        return stepRunId;
    }

    public void Dispose() => _fixture.Dispose();
}
