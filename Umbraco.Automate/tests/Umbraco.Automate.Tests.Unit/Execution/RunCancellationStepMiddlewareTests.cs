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

    [Fact]
    public async Task HandleAsync_RunNotCancelled_PassesThrough()
    {
        var runId = Guid.NewGuid();
        _runRepository
            .Setup(r => r.GetRunStatusAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationRunStatus.Running);

        var context = CreateContext(workflowData: new AutomationWorkflowData { RunId = runId });

        var (result, nextCalled) = await InvokeAsync(context);

        nextCalled.ShouldBeTrue();
        result.Proceed.ShouldBeTrue();
        context.Workflow.Status.ShouldBe(WorkflowStatus.Runnable);
    }

    [Fact]
    public async Task HandleAsync_RunCancelled_TerminatesWorkflowWithoutCallingNext()
    {
        var runId = Guid.NewGuid();
        _runRepository
            .Setup(r => r.GetRunStatusAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationRunStatus.Cancelled);

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
