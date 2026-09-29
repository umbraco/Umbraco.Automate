using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task RecoverStuckRunsAsync_DurableRun_LeavesRunAndInstanceToResume()
    {
        var (runId, instanceId) = await SeedRunAsync(
            AutomationRunStatus.Running, StepRunStatus.Sleeping, WorkflowStatus.Runnable);

        await _recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        (await db.AutomationRuns.SingleAsync(r => r.Id == runId)).Status.ShouldBe((int)AutomationRunStatus.Running);
        (await db.WorkflowInstances.SingleAsync(wi => wi.Id == instanceId)).Status.ShouldBe((int)WorkflowStatus.Runnable);
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

    public void Dispose() => _fixture.Dispose();
}
