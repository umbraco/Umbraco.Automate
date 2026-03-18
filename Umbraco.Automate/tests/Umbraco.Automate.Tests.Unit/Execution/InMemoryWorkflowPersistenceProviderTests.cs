using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Runs;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class InMemoryWorkflowPersistenceProviderTests
{
    private readonly InMemoryWorkflowPersistenceProvider _provider;

    public InMemoryWorkflowPersistenceProviderTests()
    {
        var meterFactory = new Mock<IMeterFactory>();
        meterFactory
            .Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns(new Meter("test"));

        var metrics = new AutomateMetrics(meterFactory.Object);
        var finalizer = new RunFinalizer(
            Mock.Of<IAutomationRunRepository>(),
            metrics,
            NullLogger<RunFinalizer>.Instance);

        _provider = new InMemoryWorkflowPersistenceProvider(finalizer);
    }

    [Fact]
    public async Task CreateNewWorkflow_AssignsIdAndPersists()
    {
        var workflow = new WorkflowInstance { WorkflowDefinitionId = "test", Status = WorkflowStatus.Runnable };

        var id = await _provider.CreateNewWorkflow(workflow, CancellationToken.None);

        id.ShouldNotBeNullOrEmpty();
        var retrieved = await _provider.GetWorkflowInstance(id, CancellationToken.None);
        retrieved.WorkflowDefinitionId.ShouldBe("test");
    }

    [Fact]
    public async Task PersistWorkflow_UpdatesExisting()
    {
        var workflow = new WorkflowInstance { WorkflowDefinitionId = "test", Status = WorkflowStatus.Runnable };
        var id = await _provider.CreateNewWorkflow(workflow, CancellationToken.None);

        workflow.Status = WorkflowStatus.Complete;
        await _provider.PersistWorkflow(workflow, CancellationToken.None);

        var retrieved = await _provider.GetWorkflowInstance(id, CancellationToken.None);
        retrieved.Status.ShouldBe(WorkflowStatus.Complete);
    }

    [Fact]
    public async Task GetWorkflowInstance_NotFound_Throws()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => _provider.GetWorkflowInstance("nonexistent", CancellationToken.None));
    }

    [Fact]
    public async Task GetRunnableInstances_ReturnsOnlyRunnableBeforeAsAt()
    {
        var runnable = new WorkflowInstance
        {
            WorkflowDefinitionId = "test",
            Status = WorkflowStatus.Runnable,
            NextExecution = DateTime.UtcNow.AddMinutes(-1).Ticks,
        };
        var suspended = new WorkflowInstance
        {
            WorkflowDefinitionId = "test",
            Status = WorkflowStatus.Suspended,
        };

        await _provider.CreateNewWorkflow(runnable, CancellationToken.None);
        await _provider.CreateNewWorkflow(suspended, CancellationToken.None);

        var result = await _provider.GetRunnableInstances(DateTime.UtcNow, CancellationToken.None);

        result.Count().ShouldBe(1);
    }

    [Fact]
    public async Task CreateEventSubscription_AndGetSubscriptions_RoundTrips()
    {
        var sub = new EventSubscription
        {
            EventName = "myEvent",
            EventKey = "key1",
            SubscribeAsOf = DateTime.UtcNow.AddMinutes(-1),
        };

        var id = await _provider.CreateEventSubscription(sub, CancellationToken.None);
        id.ShouldNotBeNullOrEmpty();

        var subs = await _provider.GetSubscriptions("myEvent", "key1", DateTime.UtcNow, CancellationToken.None);
        subs.Count().ShouldBe(1);
    }

    [Fact]
    public async Task TerminateSubscription_RemovesIt()
    {
        var sub = new EventSubscription
        {
            EventName = "myEvent",
            EventKey = "key1",
            SubscribeAsOf = DateTime.UtcNow.AddMinutes(-1),
        };

        var id = await _provider.CreateEventSubscription(sub, CancellationToken.None);
        await _provider.TerminateSubscription(id, CancellationToken.None);

        var subs = await _provider.GetSubscriptions("myEvent", "key1", DateTime.UtcNow, CancellationToken.None);
        subs.Count().ShouldBe(0);
    }

    [Fact]
    public async Task CreateEvent_AndMarkProcessed_Works()
    {
        var evt = new Event
        {
            EventName = "test",
            EventKey = "key",
            EventTime = DateTime.UtcNow.AddMinutes(-1),
        };

        var id = await _provider.CreateEvent(evt, CancellationToken.None);

        var runnableBefore = await _provider.GetRunnableEvents(DateTime.UtcNow, CancellationToken.None);
        runnableBefore.Count().ShouldBe(1);

        await _provider.MarkEventProcessed(id, CancellationToken.None);

        var runnableAfter = await _provider.GetRunnableEvents(DateTime.UtcNow, CancellationToken.None);
        runnableAfter.Count().ShouldBe(0);
    }

    [Fact]
    public async Task GetWorkflowInstances_FiltersByStatus()
    {
        var w1 = new WorkflowInstance { WorkflowDefinitionId = "test", Status = WorkflowStatus.Runnable };
        var w2 = new WorkflowInstance { WorkflowDefinitionId = "test", Status = WorkflowStatus.Complete };

        await _provider.CreateNewWorkflow(w1, CancellationToken.None);
        await _provider.CreateNewWorkflow(w2, CancellationToken.None);

        var runnable = await _provider.GetWorkflowInstances(WorkflowStatus.Runnable, "test", null, null, 0, 100);
        var complete = await _provider.GetWorkflowInstances(WorkflowStatus.Complete, "test", null, null, 0, 100);

        runnable.Count().ShouldBe(1);
        complete.Count().ShouldBe(1);
    }

    [Fact]
    public async Task SetSubscriptionToken_AndClear_Works()
    {
        var sub = new EventSubscription
        {
            EventName = "myEvent",
            EventKey = "key1",
            SubscribeAsOf = DateTime.UtcNow.AddMinutes(-1),
        };

        var id = await _provider.CreateEventSubscription(sub, CancellationToken.None);

        var set = await _provider.SetSubscriptionToken(id, "token-1", "worker-1", DateTime.UtcNow.AddHours(1), CancellationToken.None);
        set.ShouldBeTrue();

        // Token set means it's no longer returned by GetSubscriptions (which filters ExternalToken == null)
        var subs = await _provider.GetSubscriptions("myEvent", "key1", DateTime.UtcNow, CancellationToken.None);
        subs.Count().ShouldBe(0);

        await _provider.ClearSubscriptionToken(id, "token-1", CancellationToken.None);

        var subsAfterClear = await _provider.GetSubscriptions("myEvent", "key1", DateTime.UtcNow, CancellationToken.None);
        subsAfterClear.Count().ShouldBe(1);
    }

    [Fact]
    public void SupportsScheduledCommands_ReturnsTrue()
    {
        _provider.SupportsScheduledCommands.ShouldBeTrue();
    }

    [Fact]
    public async Task PersistErrors_StoresWithoutThrowing()
    {
        var errors = new[]
        {
            new ExecutionError { Message = "test error" },
        };

        await _provider.PersistErrors(errors, CancellationToken.None);
        // No throw = success. Errors are stored internally for diagnostics.
    }

    [Fact]
    public async Task DeepClones_PreventsMutationOfStoredInstances()
    {
        var workflow = new WorkflowInstance
        {
            WorkflowDefinitionId = "test",
            Status = WorkflowStatus.Runnable,
            Description = "original",
        };

        var id = await _provider.CreateNewWorkflow(workflow, CancellationToken.None);

        // Mutate the returned instance
        var retrieved = await _provider.GetWorkflowInstance(id, CancellationToken.None);
        retrieved.Description = "mutated";

        // The stored instance should still have the original value
        var retrieved2 = await _provider.GetWorkflowInstance(id, CancellationToken.None);
        retrieved2.Description.ShouldBe("original");
    }
}
