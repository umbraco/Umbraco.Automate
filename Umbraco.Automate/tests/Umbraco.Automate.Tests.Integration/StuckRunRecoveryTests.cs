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
/// </summary>
public class StuckRunRecoveryTests : IDisposable
{
    private readonly EfCoreTestFixture _fixture = new();
    private readonly Mock<IServerRoleAccessor> _serverRoleAccessor = new();
    private readonly EFCoreStuckRunRecovery _recovery;

    public StuckRunRecoveryTests()
    {
        _serverRoleAccessor.Setup(r => r.CurrentServerRole).Returns(ServerRole.Single);

        _recovery = new EFCoreStuckRunRecovery(
            new TestDbContextFactory(_fixture.CreateContext),
            _serverRoleAccessor.Object,
            Options.Create(new WorkflowLockOptions { LeaseDuration = TimeSpan.FromMilliseconds(200) }),
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
    public async Task RecoverStuckRunsAsync_LeaseLeftByDeadProcess_RecoversRunOnceItLapses()
    {
        var (runId, instanceId) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Running, WorkflowStatus.Runnable);
        await SeedLeaseAsync(instanceId, DateTime.UtcNow.AddMilliseconds(100));

        await _recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe((int)AutomationRunStatus.Failed);
        (await db.WorkflowInstances.SingleAsync(wi => wi.Id == instanceId)).Status.ShouldBe((int)WorkflowStatus.Terminated);
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
    public async Task RecoverStuckRunsAsync_RunCompletesDuringLeaseWait_LeavesItCompleted()
    {
        var (runId, instanceId) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Running, WorkflowStatus.Runnable);
        await SeedLeaseAsync(instanceId, DateTime.UtcNow.AddMilliseconds(100));

        var recovery = _recovery.RecoverStuckRunsAsync(CancellationToken.None);

        // Another node finishes the run and releases its lease while recovery is waiting.
        await Task.Delay(50);
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

    private async Task SeedLeaseAsync(string lockId, DateTime expiresUtc)
    {
        await using var db = _fixture.CreateContext();
        db.WorkflowLocks.Add(new WorkflowLockEntity
        {
            LockId = lockId,
            OwnerToken = Guid.NewGuid(),
            AcquiredUtc = DateTime.UtcNow,
            ExpiresUtc = expiresUtc,
        });
        await db.SaveChangesAsync();
    }

    private async Task<(Guid RunId, string InstanceId)> SeedRunAsync(
        AutomationRunStatus runStatus,
        StepRunStatus stepStatus,
        WorkflowStatus instanceStatus)
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
