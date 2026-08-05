using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Persistence.Scoping;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Persistence;
using Umbraco.Automate.Persistence.Workflows;
using Umbraco.Automate.Tests.Common.Fixtures;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="EFCoreWorkflowPersistenceProvider"/> against in-memory
/// SQLite. Cover the normalized pointer schema (pointers persisted as their own rows, delta
/// writes), full field round-trip fidelity through the JSON sub-field columns, and the lazy
/// upgrade of legacy (SchemaVersion 0) whole-instance blobs.
/// </summary>
public class WorkflowPersistenceProviderTests : IDisposable
{
    // Must match EFCoreWorkflowPersistenceProvider.JsonSettings so the legacy-blob test writes
    // a row the provider's legacy decoder can read.
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.All,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
    };

    private readonly EfCoreTestFixture _fixture;
    private readonly EFCoreWorkflowPersistenceProvider _provider;
    private readonly RunFinalizer _finalizer;

    public WorkflowPersistenceProviderTests()
    {
        _fixture = new EfCoreTestFixture();
        var dbContextFactory = new TestDbContextFactory(_fixture.CreateContext);

        var meterFactory = new Mock<IMeterFactory>();
        meterFactory.Setup(f => f.Create(It.IsAny<MeterOptions>())).Returns(new Meter("test"));
        var metrics = new AutomateMetrics(meterFactory.Object);

        var hydrationCache = new StepOutputHydrationCache(Mock.Of<IAutomationRunRepository>());
        var collectionCache = new ForEachCollectionCache(
            new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>)),
            hydrationCache);

        _finalizer = new RunFinalizer(
            Mock.Of<IAutomationRunRepository>(),
            hydrationCache,
            Mock.Of<ICoreScopeProvider>(),
            Mock.Of<IEventMessagesFactory>(),
            metrics,
            collectionCache,
            NullLogger<RunFinalizer>.Instance);

        _provider = new EFCoreWorkflowPersistenceProvider(
            dbContextFactory, _finalizer, NullLogger<EFCoreWorkflowPersistenceProvider>.Instance);
    }

    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task RoundTrip_PreservesInstanceAndAllPointerFields()
    {
        var containerStepId = Guid.NewGuid();
        var start = new DateTime(2026, 7, 7, 10, 0, 0, DateTimeKind.Utc);

        var workflow = NewWorkflow();
        var pointer = new ExecutionPointer
        {
            Id = "ptr-1",
            StepId = 3,
            Active = true,
            SleepUntil = start.AddMinutes(1),
            StartTime = start,
            EndTime = start.AddMinutes(2),
            RetryCount = 2,
            PredecessorId = "pred-1",
            EventName = "myEvent",
            EventKey = "myKey",
            EventPublished = true,
            EventData = "event-payload",
            StepName = "Step Three",
            Status = PointerStatus.Running,
            PersistenceData = new IteratorPersistenceData { ChildrenActive = true, Index = 4 },
            ContextItem = new ForEachIterationContext(null, 5, containerStepId),
            Outcome = "outcome-value",
            Children = ["child-a", "child-b"],
            Scope = ["scope-1", "scope-2"],
        };
        pointer.ExtensionAttributes["attr-key"] = "attr-value";
        workflow.ExecutionPointers.Add(pointer);

        await _provider.CreateNewWorkflow(workflow, CancellationToken.None);
        var reloaded = await _provider.GetWorkflowInstance(workflow.Id, CancellationToken.None);

        reloaded.WorkflowDefinitionId.ShouldBe("test-def");
        reloaded.Version.ShouldBe(7);
        reloaded.Status.ShouldBe(WorkflowStatus.Runnable);
        reloaded.Description.ShouldBe("a description");
        reloaded.Reference.ShouldBe("a-reference");
        reloaded.NextExecution.ShouldBe(9999L);
        reloaded.ExecutionPointers.Count.ShouldBe(1);

        var rp = reloaded.ExecutionPointers.Single();
        rp.Id.ShouldBe("ptr-1");
        rp.StepId.ShouldBe(3);
        rp.Active.ShouldBeTrue();
        rp.SleepUntil.ShouldBe(start.AddMinutes(1));
        rp.StartTime.ShouldBe(start);
        rp.EndTime.ShouldBe(start.AddMinutes(2));
        rp.RetryCount.ShouldBe(2);
        rp.PredecessorId.ShouldBe("pred-1");
        rp.EventName.ShouldBe("myEvent");
        rp.EventKey.ShouldBe("myKey");
        rp.EventPublished.ShouldBeTrue();
        rp.EventData.ShouldBe("event-payload");
        rp.StepName.ShouldBe("Step Three");
        rp.Status.ShouldBe(PointerStatus.Running);
        rp.Outcome.ShouldBe("outcome-value");
        rp.Children.ShouldBe(new[] { "child-a", "child-b" });
        rp.Scope.ShouldBe(new[] { "scope-1", "scope-2" });

        // Polymorphic sub-fields survive the JSON round-trip with their concrete types.
        rp.PersistenceData.ShouldBeOfType<IteratorPersistenceData>().Index.ShouldBe(4);
        rp.ContextItem.ShouldBe(new ForEachIterationContext(null, 5, containerStepId));
        rp.ExtensionAttributes["attr-key"].ShouldBe("attr-value");
    }

    [Fact]
    public async Task CreateNewWorkflow_NormalizesPointersIntoTable_WithSchemaVersion1()
    {
        var workflow = NewWorkflow();
        workflow.ExecutionPointers.Add(new ExecutionPointer
        {
            Id = "ptr-1",
            ContextItem = new ForEachIterationContext(null, 0, Guid.NewGuid()),
        });
        workflow.ExecutionPointers.Add(new ExecutionPointer { Id = "ptr-2" });

        await _provider.CreateNewWorkflow(workflow, CancellationToken.None);

        await using var db = _fixture.CreateContext();
        var entity = db.WorkflowInstances.Single(e => e.Id == workflow.Id);
        var pointerRows = db.WorkflowExecutionPointers.Count(p => p.WorkflowInstanceId == workflow.Id);

        entity.SchemaVersion.ShouldBe(1);
        pointerRows.ShouldBe(2);
        // Pointers are normalized out of the instance Data column.
        entity.Data.ShouldNotContain("ForEachIterationContext");
        entity.Data.ShouldNotContain("ptr-1");
    }

    [Fact]
    public async Task PersistWorkflow_WritesDelta_AddsNewAndUpdatesExistingPointers()
    {
        var workflow = NewWorkflow();
        workflow.ExecutionPointers.Add(new ExecutionPointer { Id = "ptr-1", Active = true, StepId = 0 });
        await _provider.CreateNewWorkflow(workflow, CancellationToken.None);

        // Mutate the first pointer and add a second, then persist.
        var end = new DateTime(2026, 7, 7, 11, 0, 0, DateTimeKind.Utc);
        workflow.ExecutionPointers.Single(p => p.Id == "ptr-1").Active = false;
        workflow.ExecutionPointers.Single(p => p.Id == "ptr-1").EndTime = end;
        workflow.ExecutionPointers.Add(new ExecutionPointer { Id = "ptr-2", Active = true, StepId = 1 });
        await _provider.PersistWorkflow(workflow, CancellationToken.None);

        var reloaded = await _provider.GetWorkflowInstance(workflow.Id, CancellationToken.None);
        reloaded.ExecutionPointers.Count.ShouldBe(2);

        var first = reloaded.ExecutionPointers.Single(p => p.Id == "ptr-1");
        first.Active.ShouldBeFalse();
        first.EndTime.ShouldBe(end);
        reloaded.ExecutionPointers.Single(p => p.Id == "ptr-2").Active.ShouldBeTrue();
    }

    [Fact]
    public async Task WorkflowExecutionPointers_UniqueIndex_RejectsDuplicatePointerIdForSameInstance()
    {
        var workflow = NewWorkflow();
        await _provider.CreateNewWorkflow(workflow, CancellationToken.None);

        await using var db = _fixture.CreateContext();
        db.WorkflowExecutionPointers.Add(new WorkflowExecutionPointerEntity
        {
            WorkflowInstanceId = workflow.Id,
            PointerId = "dup-ptr",
        });
        await db.SaveChangesAsync();

        db.WorkflowExecutionPointers.Add(new WorkflowExecutionPointerEntity
        {
            WorkflowInstanceId = workflow.Id,
            PointerId = "dup-ptr",
        });

        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task PersistWorkflow_ConcurrentPointerCollision_DiscardsLoserInsteadOfThrowing()
    {
        var workflow = NewWorkflow();
        await _provider.CreateNewWorkflow(workflow, CancellationToken.None);
        workflow.ExecutionPointers.Add(new ExecutionPointer { Id = "race-ptr", StepId = 1 });

        // Deterministically reproduce the race window the unique index (and this catch) guards:
        // between the provider's own LoadTrackedAsync (which finds no "race-ptr" pointer yet)
        // and its SaveChangesAsync actually issuing the INSERT, a *different* writer commits a
        // pointer row with the same (WorkflowInstanceId, PointerId) first. EF Core's
        // DbContext.SavingChanges event fires synchronously right before that INSERT is
        // executed, so it's used here (test-side only) to inject the other writer's commit into
        // that exact gap - true OS-thread concurrency over an in-memory SQLite database isn't
        // reliably interleaved by Task.WhenAll, since both the query and the write complete
        // near-instantly with no real I/O wait to yield on.
        var racingFactory = new HookingDbContextFactory(_fixture.CreateContext, () =>
        {
            using var other = _fixture.CreateContext();
            other.WorkflowExecutionPointers.Add(new WorkflowExecutionPointerEntity
            {
                WorkflowInstanceId = workflow.Id,
                PointerId = "race-ptr",
                StepId = 99,
            });
            other.SaveChanges();
        });
        var racingProvider = new EFCoreWorkflowPersistenceProvider(
            racingFactory, _finalizer, NullLogger<EFCoreWorkflowPersistenceProvider>.Instance);

        // The provider must not surface the other writer's DbUpdateException to its caller.
        await Should.NotThrowAsync(() => racingProvider.PersistWorkflow(workflow, CancellationToken.None));

        // The unique index prevents a duplicate row from ever landing: the other writer's row
        // survives, and this call's insert was silently discarded rather than retried or thrown.
        await using var verify = _fixture.CreateContext();
        var rows = verify.WorkflowExecutionPointers
            .Where(p => p.WorkflowInstanceId == workflow.Id && p.PointerId == "race-ptr")
            .ToList();
        rows.Count.ShouldBe(1);
        rows.Single().StepId.ShouldBe(99);
    }

    // Wraps context creation so a one-shot callback fires on DbContext.SavingChanges - the
    // synchronous event EF Core raises immediately before a save's DB commands execute -
    // letting a test inject another writer's commit into the exact load-then-save gap the
    // provider's DbUpdateException catch defends against.
    private sealed class HookingDbContextFactory : IDetachedDbContextFactory<UmbracoAutomateDbContext>
    {
        private readonly Func<UmbracoAutomateDbContext> _inner;
        private readonly Action _onSavingChanges;

        public HookingDbContextFactory(Func<UmbracoAutomateDbContext> inner, Action onSavingChanges)
        {
            _inner = inner;
            _onSavingChanges = onSavingChanges;
        }

        public UmbracoAutomateDbContext CreateDbContext()
        {
            var context = _inner();
            context.SavingChanges += (_, _) => _onSavingChanges();
            return context;
        }
    }

    [Fact]
    public async Task LegacyBlob_IsReadViaFallback_ThenUpgradedOnNextPersist()
    {
        // Seed a legacy (SchemaVersion 0) row: the whole WorkflowInstance serialized into Data,
        // pointers inlined, no pointer-table rows.
        var legacy = NewWorkflow();
        legacy.ExecutionPointers.Add(new ExecutionPointer
        {
            Id = "legacy-ptr",
            StepId = 2,
            Active = true,
            ContextItem = new ForEachIterationContext(null, 1, Guid.NewGuid()),
        });

        await using (var seed = _fixture.CreateContext())
        {
            seed.WorkflowInstances.Add(new WorkflowInstanceEntity
            {
                Id = legacy.Id,
                WorkflowDefinitionId = legacy.WorkflowDefinitionId,
                Version = legacy.Version,
                Status = (int)legacy.Status,
                Description = legacy.Description,
                Reference = legacy.Reference,
                CreateTime = legacy.CreateTime,
                NextExecution = legacy.NextExecution,
                SchemaVersion = 0,
                Data = JsonConvert.SerializeObject(legacy, JsonSettings),
            });
            await seed.SaveChangesAsync();
        }

        // Read via the legacy fallback.
        var read = await _provider.GetWorkflowInstance(legacy.Id, CancellationToken.None);
        read.WorkflowDefinitionId.ShouldBe("test-def");
        read.ExecutionPointers.Count.ShouldBe(1);
        read.ExecutionPointers.Single().Id.ShouldBe("legacy-ptr");

        // Persisting rewrites it in the normalized format.
        await _provider.PersistWorkflow(read, CancellationToken.None);

        await using var db = _fixture.CreateContext();
        var entity = db.WorkflowInstances.Single(e => e.Id == legacy.Id);
        entity.SchemaVersion.ShouldBe(1);
        db.WorkflowExecutionPointers.Count(p => p.WorkflowInstanceId == legacy.Id).ShouldBe(1);
        entity.Data.ShouldNotContain("legacy-ptr");
    }

    private static WorkflowInstance NewWorkflow() => new()
    {
        Id = Guid.NewGuid().ToString(),
        WorkflowDefinitionId = "test-def",
        Version = 7,
        Status = WorkflowStatus.Runnable,
        Description = "a description",
        Reference = "a-reference",
        CreateTime = new DateTime(2026, 7, 7, 9, 0, 0, DateTimeKind.Utc),
        NextExecution = 9999L,
        Data = new Dictionary<string, object> { ["foo"] = "bar" },
        ExecutionPointers = new ExecutionPointerCollection(4),
    };
}
