using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Runs;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class RunCancellationStepMiddlewareTests : IDisposable
{
    private readonly Mock<IAutomationRunRepository> _runRepository = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly RunCancellationStepMiddleware _middleware;

    public RunCancellationStepMiddlewareTests()
    {
        _middleware = new RunCancellationStepMiddleware(
            _runRepository.Object,
            _cache,
            Mock.Of<ILogger<RunCancellationStepMiddleware>>());
    }

    [Fact]
    public async Task HandleAsync_WorkflowDataIsNotAutomationWorkflowData_PassesThrough()
    {
        var context = CreateContext(workflowData: new object());

        var (result, nextCalled) = await InvokeAsync(context);

        nextCalled.ShouldBeTrue();
        result.Proceed.ShouldBeTrue();
        _runRepository.Verify(
            r => r.GetRunStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(AutomationRunStatus.Running)]
    [InlineData(AutomationRunStatus.Pending)]
    [InlineData(AutomationRunStatus.Suspended)]
    public async Task HandleAsync_RunNotTerminal_PassesThrough(AutomationRunStatus status)
    {
        var runId = Guid.NewGuid();
        _runRepository
            .Setup(r => r.GetRunStatusAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var context = CreateContext(workflowData: new AutomationWorkflowData { RunId = runId });

        var (result, nextCalled) = await InvokeAsync(context);

        nextCalled.ShouldBeTrue();
        result.Proceed.ShouldBeTrue();
        context.Workflow.Status.ShouldBe(WorkflowStatus.Runnable);
    }

    // Failed covers the reported case: startup recovery failed a run the engine went on to resume,
    // re-running an AI step behind a run the backoffice showed as failed and would not terminate.
    [Theory]
    [InlineData(AutomationRunStatus.Cancelled)]
    [InlineData(AutomationRunStatus.Failed)]
    [InlineData(AutomationRunStatus.Completed)]
    [InlineData(AutomationRunStatus.Rejected)]
    public async Task HandleAsync_RunTerminal_TerminatesWorkflowWithoutCallingNext(AutomationRunStatus status)
    {
        var runId = Guid.NewGuid();
        _runRepository
            .Setup(r => r.GetRunStatusAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var context = CreateContext(workflowData: new AutomationWorkflowData { RunId = runId });
        context.PersistenceData = "persisted-pointer-data";

        var (result, nextCalled) = await InvokeAsync(context);

        nextCalled.ShouldBeFalse();
        context.Workflow.Status.ShouldBe(WorkflowStatus.Terminated);
        context.Workflow.CompleteTime.ShouldNotBeNull();
        result.Proceed.ShouldBeFalse();
        result.PersistenceData.ShouldBe(context.PersistenceData);
    }

    private async Task<(ExecutionResult Result, bool NextCalled)> InvokeAsync(IStepExecutionContext context)
    {
        var nextCalled = false;
        WorkflowStepDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(ExecutionResult.Next());
        };

        var result = await _middleware.HandleAsync(context, Mock.Of<IStepBody>(), next);
        return (result, nextCalled);
    }

    private static IStepExecutionContext CreateContext(object workflowData) => new StepExecutionContext
    {
        Workflow = new WorkflowInstance
        {
            Id = Guid.NewGuid().ToString(),
            Data = workflowData,
            Status = WorkflowStatus.Runnable,
        },
        CancellationToken = CancellationToken.None,
    };

    public void Dispose() => _cache.Dispose();
}
