using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class RunFinalizerTests
{
    private readonly Mock<IAutomationRunRepository> _runRepo = new();
    private readonly RunFinalizer _finalizer;

    public RunFinalizerTests()
    {
        var meterFactory = new Mock<IMeterFactory>();
        meterFactory
            .Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts.Name));
        var metrics = new AutomateMetrics(meterFactory.Object);

        var scope = new Mock<ICoreScope>();
        scope.Setup(s => s.Notifications).Returns(Mock.Of<Umbraco.Cms.Core.Events.IScopedNotificationPublisher>());

        var scopeProvider = new Mock<ICoreScopeProvider>();
        scopeProvider
            .Setup(s => s.CreateCoreScope(
                It.IsAny<System.Data.IsolationLevel>(),
                It.IsAny<Umbraco.Cms.Core.Scoping.RepositoryCacheMode>(),
                It.IsAny<Umbraco.Cms.Core.Events.IEventDispatcher?>(),
                It.IsAny<Umbraco.Cms.Core.Events.IScopedNotificationPublisher?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns(scope.Object);

        var eventMessagesFactory = new Mock<IEventMessagesFactory>();
        eventMessagesFactory.Setup(f => f.Get()).Returns(new EventMessages());

        var collectionCache = new ForEachCollectionCache(new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>)));

        _finalizer = new RunFinalizer(
            _runRepo.Object,
            new StepOutputHydrationCache(_runRepo.Object),
            scopeProvider.Object,
            eventMessagesFactory.Object,
            metrics,
            collectionCache,
            NullLogger<RunFinalizer>.Instance);
    }

    private static WorkflowInstance BuildWorkflow(WorkflowStatus status, Guid runId)
    {
        return new WorkflowInstance
        {
            Status = status,
            Data = new AutomationWorkflowData
            {
                RunId = runId,
                AutomationId = Guid.NewGuid(),
                AutomationAlias = "test",
            },
        };
    }

    private static AutomationRun BuildRun(AutomationRunStatus status)
    {
        return new AutomationRun
        {
            AutomationId = Guid.NewGuid(),
            AutomationVersion = 1,
            WorkspaceId = Guid.NewGuid(),
            ServiceAccountKey = Guid.NewGuid(),
            InitiatedBy = "test",
            Status = status,
            StartedUtc = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task Suspended_TransitionsRunningToSuspended()
    {
        var run = BuildRun(AutomationRunStatus.Running);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);

        await _finalizer.TryFinalizeAsync(BuildWorkflow(WorkflowStatus.Suspended, Guid.NewGuid()), CancellationToken.None);

        run.Status.ShouldBe(AutomationRunStatus.Suspended);
        run.CompletedUtc.ShouldBeNull();
        _runRepo.Verify(r => r.SaveAsync(run, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Suspended_NoOpWhenAlreadySuspended()
    {
        var run = BuildRun(AutomationRunStatus.Suspended);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);

        await _finalizer.TryFinalizeAsync(BuildWorkflow(WorkflowStatus.Suspended, Guid.NewGuid()), CancellationToken.None);

        _runRepo.Verify(r => r.SaveAsync(It.IsAny<AutomationRun>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Suspended_NoOpWhenRunNotFound()
    {
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((AutomationRun?)null);

        await _finalizer.TryFinalizeAsync(BuildWorkflow(WorkflowStatus.Suspended, Guid.NewGuid()), CancellationToken.None);

        _runRepo.Verify(r => r.SaveAsync(It.IsAny<AutomationRun>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Complete_FinalizesSuspendedRunAsCompleted()
    {
        // A suspended run whose workflow completes (e.g. after approval) should reach Completed.
        var run = BuildRun(AutomationRunStatus.Suspended);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);

        await _finalizer.TryFinalizeAsync(BuildWorkflow(WorkflowStatus.Complete, Guid.NewGuid()), CancellationToken.None);

        run.Status.ShouldBe(AutomationRunStatus.Completed);
        run.CompletedUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Runnable_NoOp()
    {
        // Runnable persists happen constantly during normal execution — finalizer must
        // not do any DB work on them.
        await _finalizer.TryFinalizeAsync(BuildWorkflow(WorkflowStatus.Runnable, Guid.NewGuid()), CancellationToken.None);

        _runRepo.Verify(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Complete_FinalizesRunningRunAsCompleted()
    {
        var run = BuildRun(AutomationRunStatus.Running);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);

        await _finalizer.TryFinalizeAsync(BuildWorkflow(WorkflowStatus.Complete, Guid.NewGuid()), CancellationToken.None);

        run.Status.ShouldBe(AutomationRunStatus.Completed);
        run.CompletedUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Terminated_FinalizesAsFailed()
    {
        var run = BuildRun(AutomationRunStatus.Running);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);

        await _finalizer.TryFinalizeAsync(BuildWorkflow(WorkflowStatus.Terminated, Guid.NewGuid()), CancellationToken.None);

        run.Status.ShouldBe(AutomationRunStatus.Failed);
        run.Error.ShouldBe("Workflow terminated");
    }

    [Fact]
    public async Task Complete_NoOpWhenAlreadyCompleted()
    {
        var run = BuildRun(AutomationRunStatus.Completed);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);

        await _finalizer.TryFinalizeAsync(BuildWorkflow(WorkflowStatus.Complete, Guid.NewGuid()), CancellationToken.None);

        _runRepo.Verify(r => r.SaveAsync(It.IsAny<AutomationRun>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
