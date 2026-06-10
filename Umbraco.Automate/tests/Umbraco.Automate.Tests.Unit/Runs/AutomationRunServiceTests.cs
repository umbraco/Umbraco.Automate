using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Core.Events;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Tests.Unit.Runs;

public class AutomationRunServiceTests
{
    private readonly Mock<IAutomationRunRepository> _runRepo = new();
    private readonly Mock<IWorkflowHost> _workflowHost = new();
    private readonly Mock<IEventAggregator> _eventAggregator = new();
    private readonly AutomationRunService _service;

    public AutomationRunServiceTests()
    {
        _eventAggregator
            .Setup(e => e.PublishAsync(It.IsAny<Umbraco.Automate.Core.Notifications.AutomationRunResumedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new AutomationRunService(
            _runRepo.Object,
            _workflowHost.Object,
            _eventAggregator.Object,
            NullLogger<AutomationRunService>.Instance);
    }

    private static AutomationRun BuildRun(AutomationRunStatus status, string? workflowInstanceId = "instance-1")
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
            WorkflowInstanceId = workflowInstanceId,
        };
    }

    // --- Suspend ---

    [Fact]
    public async Task SuspendRun_NotFound_ReturnsNotFound()
    {
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((AutomationRun?)null);

        var result = await _service.SuspendRunAsync(Guid.NewGuid());

        result.ShouldBe(RunLifecycleResult.NotFound);
    }

    [Fact]
    public async Task SuspendRun_AlreadySuspended_ReturnsInvalidState()
    {
        var run = BuildRun(AutomationRunStatus.Suspended);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);

        var result = await _service.SuspendRunAsync(Guid.NewGuid());

        result.ShouldBe(RunLifecycleResult.InvalidState);
        _workflowHost.Verify(h => h.SuspendWorkflow(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SuspendRun_NoInstanceId_ReturnsNoWorkflowInstance()
    {
        var run = BuildRun(AutomationRunStatus.Running, workflowInstanceId: null);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);

        var result = await _service.SuspendRunAsync(Guid.NewGuid());

        result.ShouldBe(RunLifecycleResult.NoWorkflowInstance);
    }

    [Fact]
    public async Task SuspendRun_Running_SuspendsAndUpdatesStatus()
    {
        var run = BuildRun(AutomationRunStatus.Running);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);

        var result = await _service.SuspendRunAsync(Guid.NewGuid());

        result.ShouldBe(RunLifecycleResult.Success);
        _workflowHost.Verify(h => h.SuspendWorkflow("instance-1"), Times.Once);
        run.Status.ShouldBe(AutomationRunStatus.Suspended);
        _runRepo.Verify(r => r.SaveAsync(run, It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Resume ---

    [Fact]
    public async Task ResumeRun_NotSuspended_ReturnsInvalidState()
    {
        var run = BuildRun(AutomationRunStatus.Running);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);

        var result = await _service.ResumeRunAsync(Guid.NewGuid());

        result.ShouldBe(RunLifecycleResult.InvalidState);
        _workflowHost.Verify(h => h.ResumeWorkflow(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResumeRun_Suspended_ResumesAndUpdatesStatus()
    {
        var run = BuildRun(AutomationRunStatus.Suspended);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);

        var result = await _service.ResumeRunAsync(Guid.NewGuid());

        result.ShouldBe(RunLifecycleResult.Success);
        _workflowHost.Verify(h => h.ResumeWorkflow("instance-1"), Times.Once);
        run.Status.ShouldBe(AutomationRunStatus.Running);
    }

    // --- Terminate ---

    [Fact]
    public async Task TerminateRun_AlreadyCompleted_ReturnsInvalidState()
    {
        var run = BuildRun(AutomationRunStatus.Completed);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);

        var result = await _service.TerminateRunAsync(Guid.NewGuid());

        result.ShouldBe(RunLifecycleResult.InvalidState);
        _workflowHost.Verify(h => h.TerminateWorkflow(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TerminateRun_Running_TerminatesAndMarksCancelled()
    {
        var run = BuildRun(AutomationRunStatus.Running);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);

        var result = await _service.TerminateRunAsync(Guid.NewGuid());

        result.ShouldBe(RunLifecycleResult.Success);
        _workflowHost.Verify(h => h.TerminateWorkflow("instance-1"), Times.Once);
        run.Status.ShouldBe(AutomationRunStatus.Cancelled);
        run.CompletedUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task TerminateRun_Suspended_Terminates()
    {
        var run = BuildRun(AutomationRunStatus.Suspended);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);

        var result = await _service.TerminateRunAsync(Guid.NewGuid());

        result.ShouldBe(RunLifecycleResult.Success);
        _workflowHost.Verify(h => h.TerminateWorkflow("instance-1"), Times.Once);
        run.Status.ShouldBe(AutomationRunStatus.Cancelled);
    }

    [Fact]
    public async Task TerminateRun_WritesCancelledBeforeCallingWorkflowHost()
    {
        // If we called TerminateWorkflow first, RunFinalizer would observe the run as
        // still Running and dispatch AutomationRunCompletedNotification with Failed
        // before the service overwrites with Cancelled. Writing Cancelled first lets
        // the finalizer's early-return guard short-circuit.
        var run = BuildRun(AutomationRunStatus.Running);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);

        var callOrder = new List<string>();
        _runRepo
            .Setup(r => r.SaveAsync(It.IsAny<AutomationRun>(), It.IsAny<CancellationToken>()))
            .Callback<AutomationRun, CancellationToken>((r, _) => callOrder.Add($"save:{r.Status}"))
            .ReturnsAsync(run);
        _workflowHost
            .Setup(h => h.TerminateWorkflow(It.IsAny<string>()))
            .Callback<string>(_ => callOrder.Add("terminate"))
            .ReturnsAsync(true);

        await _service.TerminateRunAsync(Guid.NewGuid());

        callOrder.ShouldBe(["save:Cancelled", "terminate"]);
    }
}
