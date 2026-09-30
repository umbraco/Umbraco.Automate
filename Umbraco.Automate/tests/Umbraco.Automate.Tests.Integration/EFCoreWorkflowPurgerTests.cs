using Umbraco.Automate.Persistence.Workflows;
using Umbraco.Automate.Tests.Common.Fixtures;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="EFCoreWorkflowPurger"/> against in-memory SQLite: finished engine
/// state older than the cutoff is deleted, anything the engine could still pick up is left alone.
/// </summary>
public class EFCoreWorkflowPurgerTests : IDisposable
{
    private readonly EfCoreTestFixture _fixture = new();
    private readonly EFCoreWorkflowPurger _purger;

    public EFCoreWorkflowPurgerTests()
    {
        _purger = new EFCoreWorkflowPurger(new TestDbContextFactory(_fixture.CreateContext));
    }

    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(WorkflowStatus.Complete)]
    [InlineData(WorkflowStatus.Terminated)]
    public async Task PurgeWorkflows_FinishedInstanceOlderThanCutoff_DeletesInstancePointersAndSubscriptions(WorkflowStatus status)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var id = await SeedInstanceAsync(status, cutoff.AddDays(-1));

        await _purger.PurgeWorkflows(status, cutoff);

        await using var db = _fixture.CreateContext();
        db.WorkflowInstances.Any(w => w.Id == id).ShouldBeFalse();
        db.WorkflowExecutionPointers.Any(p => p.WorkflowInstanceId == id).ShouldBeFalse();
        db.EventSubscriptions.Any(s => s.WorkflowId == id).ShouldBeFalse();
    }

    [Fact]
    public async Task PurgeWorkflows_FinishedInstanceNewerThanCutoff_KeepsIt()
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var id = await SeedInstanceAsync(WorkflowStatus.Complete, cutoff.AddDays(1));

        await _purger.PurgeWorkflows(WorkflowStatus.Complete, cutoff);

        await using var db = _fixture.CreateContext();
        db.WorkflowInstances.Any(w => w.Id == id).ShouldBeTrue();
        db.WorkflowExecutionPointers.Any(p => p.WorkflowInstanceId == id).ShouldBeTrue();
    }

    [Fact]
    public async Task PurgeWorkflows_OnlyPurgesTheRequestedStatus()
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var terminated = await SeedInstanceAsync(WorkflowStatus.Terminated, cutoff.AddDays(-1));

        await _purger.PurgeWorkflows(WorkflowStatus.Complete, cutoff);

        await using var db = _fixture.CreateContext();
        db.WorkflowInstances.Any(w => w.Id == terminated).ShouldBeTrue();
    }

    [Theory]
    [InlineData(WorkflowStatus.Runnable)]
    [InlineData(WorkflowStatus.Suspended)]
    public async Task PurgeWorkflows_LiveStatus_IsRejectedAndNothingIsDeleted(WorkflowStatus status)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var id = await SeedInstanceAsync(status, cutoff.AddDays(-1));

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => _purger.PurgeWorkflows(status, cutoff));

        await using var db = _fixture.CreateContext();
        db.WorkflowInstances.Any(w => w.Id == id).ShouldBeTrue();
    }

    [Fact]
    public async Task PurgeWorkflows_DeletesOnlyProcessedEventsOlderThanCutoff()
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var oldProcessed = Guid.NewGuid().ToString();
        var oldUnprocessed = Guid.NewGuid().ToString();
        var recentProcessed = Guid.NewGuid().ToString();

        await using (var seed = _fixture.CreateContext())
        {
            seed.Events.AddRange(
                NewEvent(oldProcessed, cutoff.AddDays(-1), isProcessed: true),
                NewEvent(oldUnprocessed, cutoff.AddDays(-1), isProcessed: false),
                NewEvent(recentProcessed, cutoff.AddDays(1), isProcessed: true));
            await seed.SaveChangesAsync();
        }

        await _purger.PurgeWorkflows(WorkflowStatus.Complete, cutoff);

        await using var db = _fixture.CreateContext();
        db.Events.Any(e => e.Id == oldProcessed).ShouldBeFalse();
        db.Events.Any(e => e.Id == oldUnprocessed).ShouldBeTrue();
        db.Events.Any(e => e.Id == recentProcessed).ShouldBeTrue();
    }

    private static EventEntity NewEvent(string id, DateTime eventTime, bool isProcessed) => new()
    {
        Id = id,
        EventName = "test",
        EventKey = "key",
        EventTime = eventTime,
        IsProcessed = isProcessed,
    };

    private async Task<string> SeedInstanceAsync(WorkflowStatus status, DateTime completeTime)
    {
        var id = Guid.NewGuid().ToString();

        await using var db = _fixture.CreateContext();

        db.WorkflowInstances.Add(new WorkflowInstanceEntity
        {
            Id = id,
            WorkflowDefinitionId = $"automate-{Guid.NewGuid()}-v1",
            Version = 1,
            Status = (int)status,
            CreateTime = completeTime.AddMinutes(-1),
            CompleteTime = completeTime,
            SchemaVersion = 1,
            Data = "{}",
        });

        db.WorkflowExecutionPointers.Add(new WorkflowExecutionPointerEntity
        {
            WorkflowInstanceId = id,
            PointerId = Guid.NewGuid().ToString(),
        });

        db.EventSubscriptions.Add(new EventSubscriptionEntity
        {
            Id = Guid.NewGuid().ToString(),
            WorkflowId = id,
            ExecutionPointerId = Guid.NewGuid().ToString(),
            EventName = "test",
            EventKey = "key",
        });

        await db.SaveChangesAsync();
        return id;
    }
}
