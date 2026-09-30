using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Events;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Tests.Unit.Runs;

public class AutomationRunServiceTests
{
    private readonly Mock<IAutomationRunRepository> _runRepo = new();
    private readonly Mock<IWorkflowHost> _workflowHost = new();
    private readonly Mock<IEventAggregator> _eventAggregator = new();
    private readonly RunDataSanitizer _sanitizer = new(
        new ActionCollection(() => []),
        new ControlFlowCollection(() => []),
        NullLogger<RunDataSanitizer>.Instance);
    private readonly AutomationRunService _service;

    public AutomationRunServiceTests()
    {
        _eventAggregator
            .Setup(e => e.PublishAsync(It.IsAny<Umbraco.Automate.Core.Notifications.AutomationRunResumedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new AutomationRunService(
            _runRepo.Object,
            _sanitizer,
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

    // --- Run data ---

    [Fact]
    public async Task GetStepRunData_ReturnsMaskedPrettyPrintedInputAndOutput()
    {
        var stored = GivenStoredStepRunData(
            inputData: """{"url":"https://example.com","headers":[{"key":"Authorization","value":"Bearer abc"}]}""",
            outputData: """{"statusCode":200,"token":"xyz"}""");

        var data = await _service.GetStepRunDataAsync(stored.RunId, stored.StepRunId);

        data.ShouldNotBeNull();
        data.RunId.ShouldBe(stored.RunId);
        data.StepRunId.ShouldBe(stored.StepRunId);
        data.AutomationId.ShouldBe(stored.AutomationId);
        data.ActionAlias.ShouldBe(stored.ActionAlias);
        data.InputTruncated.ShouldBeFalse();
        data.OutputTruncated.ShouldBeFalse();
        data.Input!.ShouldNotContain("Bearer abc");
        JsonNode.Parse(data.Input!)!["url"]!.GetValue<string>().ShouldBe("https://example.com");
        var output = JsonNode.Parse(data.Output!)!;
        output["statusCode"]!.GetValue<int>().ShouldBe(200);
        output["token"]!.GetValue<string>().ShouldBe(SensitiveDataMasker.MaskedValue);
        data.Output!.ShouldContain(Environment.NewLine);
    }

    [Fact]
    public async Task GetStepRunData_OffloadedOutput_IsReturnedInFull()
    {
        // An output above the inline threshold is offloaded from the workflow data; the step run
        // record is its store, so it is served from there like any other output.
        var large = new string('a', 40_000);
        var stored = GivenStoredStepRunData(inputData: null, outputData: $$"""{"body":"{{large}}"}""");

        var data = await _service.GetStepRunDataAsync(stored.RunId, stored.StepRunId);

        data.ShouldNotBeNull();
        data.OutputTruncated.ShouldBeFalse();
        JsonNode.Parse(data.Output!)!["body"]!.GetValue<string>().ShouldBe(large);
        data.Input.ShouldBeNull();
    }

    [Fact]
    public async Task GetStepRunData_OversizedOutput_IsTruncatedAndFlagged()
    {
        var huge = new string('a', IRunDataSanitizer.MaxValueLength + 1);
        var stored = GivenStoredStepRunData(inputData: "{}", outputData: $$"""{"body":"{{huge}}"}""");

        var data = await _service.GetStepRunDataAsync(stored.RunId, stored.StepRunId);

        data.ShouldNotBeNull();
        data.OutputTruncated.ShouldBeTrue();
        data.Output!.Length.ShouldBe(IRunDataSanitizer.MaxValueLength);
        data.InputTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task GetStepRunData_UnknownStepRun_ReturnsNull()
    {
        var data = await _service.GetStepRunDataAsync(Guid.NewGuid(), Guid.NewGuid());

        data.ShouldBeNull();
    }

    [Fact]
    public async Task GetTriggerData_ReturnsMaskedTriggerData()
    {
        var runId = Guid.NewGuid();
        var automationId = Guid.NewGuid();
        _runRepo
            .Setup(r => r.GetTriggerDataAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredRunTriggerData
            {
                RunId = runId,
                AutomationId = automationId,
                TriggerData = """{"headers":{"X-Api-Key":"k"},"contentId":42}""",
            });

        var data = await _service.GetTriggerDataAsync(runId);

        data.ShouldNotBeNull();
        data.RunId.ShouldBe(runId);
        data.AutomationId.ShouldBe(automationId);
        data.TriggerDataTruncated.ShouldBeFalse();
        var node = JsonNode.Parse(data.TriggerData!)!;
        node["headers"]!["X-Api-Key"]!.GetValue<string>().ShouldBe(SensitiveDataMasker.MaskedValue);
        node["contentId"]!.GetValue<int>().ShouldBe(42);
    }

    [Fact]
    public async Task GetTriggerData_NoTriggerData_ReturnsNullValue()
    {
        var runId = Guid.NewGuid();
        _runRepo
            .Setup(r => r.GetTriggerDataAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredRunTriggerData { RunId = runId, AutomationId = Guid.NewGuid() });

        var data = await _service.GetTriggerDataAsync(runId);

        data.ShouldNotBeNull();
        data.TriggerData.ShouldBeNull();
        data.TriggerDataTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task GetTriggerData_UnknownRun_ReturnsNull()
    {
        var data = await _service.GetTriggerDataAsync(Guid.NewGuid());

        data.ShouldBeNull();
    }

    private StoredStepRunData GivenStoredStepRunData(string? inputData, string? outputData)
    {
        var stored = new StoredStepRunData
        {
            RunId = Guid.NewGuid(),
            StepRunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            ActionAlias = "test.action",
            InputData = inputData,
            OutputData = outputData,
        };
        _runRepo
            .Setup(r => r.GetStepRunDataAsync(stored.RunId, stored.StepRunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);
        return stored;
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
        _workflowHost.Setup(h => h.ResumeWorkflow("instance-1")).ReturnsAsync(true);

        var result = await _service.ResumeRunAsync(Guid.NewGuid());

        result.ShouldBe(RunLifecycleResult.Success);
        _workflowHost.Verify(h => h.ResumeWorkflow("instance-1"), Times.Once);
        run.Status.ShouldBe(AutomationRunStatus.Running);
    }

    [Fact]
    public async Task ResumeRun_WorkflowNotSuspended_ReturnsInvalidStateAndStaysSuspended()
    {
        // A run waiting for an approval is Suspended, but its workflow is still Runnable, so
        // WorkflowCore refuses to resume it — only the decision can.
        var run = BuildRun(AutomationRunStatus.Suspended);
        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _workflowHost.Setup(h => h.ResumeWorkflow("instance-1")).ReturnsAsync(false);

        var result = await _service.ResumeRunAsync(Guid.NewGuid());

        result.ShouldBe(RunLifecycleResult.InvalidState);
        run.Status.ShouldBe(AutomationRunStatus.Suspended);
        _runRepo.Verify(r => r.SaveAsync(It.IsAny<AutomationRun>(), It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task TerminateRun_WithWaitingForInputStep_MarksStepCancelled()
    {
        // A "Request Approval" step suspended on WaitForInput has no next step running, so
        // RunCancellationStepMiddleware never observes it, and RunFinalizer only ever sweeps
        // steps left in Running — WaitingForInput falls through both nets. TerminateRunAsync
        // must close this gap itself so the step doesn't haunt the pending-approvals dashboard
        // forever after its parent run is dead.
        var run = BuildRun(AutomationRunStatus.Running);
        var stepRun = new StepRun
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            StepId = Guid.NewGuid(),
            ActionAlias = "Umbraco.Automate.RequestApproval",
            Status = StepRunStatus.WaitingForInput,
            StartedUtc = DateTime.UtcNow,
        };
        run.StepRuns.Add(stepRun);

        _runRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _runRepo
            .Setup(r => r.UpdateStepRunAsync(It.IsAny<StepRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StepRun sr, CancellationToken _) => sr);

        var result = await _service.TerminateRunAsync(Guid.NewGuid());

        result.ShouldBe(RunLifecycleResult.Success);

        // Not left dangling, and not silently faked as a real approval outcome.
        stepRun.Status.ShouldBe(StepRunStatus.Cancelled);
        stepRun.Status.ShouldNotBe(StepRunStatus.WaitingForInput);
        stepRun.Status.ShouldNotBe(StepRunStatus.Completed);
        stepRun.CompletedUtc.ShouldNotBeNull();

        _runRepo.Verify(
            r => r.UpdateStepRunAsync(
                It.Is<StepRun>(s => s.Id == stepRun.Id && s.Status == StepRunStatus.Cancelled),
                It.IsAny<CancellationToken>()),
            Times.Once);
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
