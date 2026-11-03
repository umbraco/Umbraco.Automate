using Microsoft.Extensions.DependencyInjection;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Extensions;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Primitives;
using WorkflowCore.Services.ErrorHandlers;

namespace Umbraco.Automate.Tests.Unit.Execution;

/// <summary>
/// <see cref="AutomateRetryHandler"/> replaces WorkflowCore's Retry handler: a step failure that
/// retrying cannot fix goes to WorkflowCore's Terminate handler, anything else is retried as before.
/// </summary>
public class AutomateRetryHandlerTests
{
    private readonly AutomateRetryHandler _handler;

    public AutomateRetryHandlerTests()
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _handler = new AutomateRetryHandler(
            new RetryHandler(dateTimeProvider.Object, new WorkflowOptions(new ServiceCollection())),
            new TerminateHandler(Mock.Of<ILifeCycleEventPublisher>(), dateTimeProvider.Object));
    }

    [Fact]
    public void Type_IsRetry()
        => _handler.Type.ShouldBe(WorkflowErrorHandling.Retry);

    [Fact]
    public void NonRetryableFailure_TerminatesTheWorkflow()
    {
        var (workflow, _) = Handle(new NonRetryableStepFailureException(new InvalidOperationException("bad")));

        workflow.Status.ShouldBe(WorkflowStatus.Terminated);
    }

    [Fact]
    public void NonRetryableFailure_DoesNotScheduleARetry()
    {
        var (_, pointer) = Handle(new NonRetryableStepFailureException(new InvalidOperationException("bad")));

        pointer.RetryCount.ShouldBe(0);
    }

    [Fact]
    public void OtherFailure_SchedulesARetry()
    {
        var (_, pointer) = Handle(new HttpRequestException("unavailable"));

        pointer.RetryCount.ShouldBe(1);
    }

    [Fact]
    public void OtherFailure_LeavesTheWorkflowRunnable()
    {
        var (workflow, _) = Handle(new HttpRequestException("unavailable"));

        workflow.Status.ShouldBe(WorkflowStatus.Runnable);
    }

    // --- Registration ---

    [Fact]
    public void ReplaceWorkflowRetryHandler_LeavesASingleRetryHandler()
    {
        var handlers = ResolveHandlers();

        handlers.Count(h => h.Type == WorkflowErrorHandling.Retry).ShouldBe(1);
    }

    [Fact]
    public void ReplaceWorkflowRetryHandler_MakesAutomateRetryHandlerTheRetryHandler()
    {
        var handlers = ResolveHandlers();

        handlers.Single(h => h.Type == WorkflowErrorHandling.Retry).ShouldBeOfType<AutomateRetryHandler>();
    }

    [Fact]
    public void ReplaceWorkflowRetryHandler_KeepsTheOtherWorkflowCoreHandlers()
    {
        var handlers = ResolveHandlers();

        handlers.Select(h => h.Type).Distinct().Count().ShouldBe(4);
    }

    [Fact]
    public void ReplaceWorkflowRetryHandler_BeforeAddWorkflow_Throws()
    {
        var services = new ServiceCollection();

        Should.Throw<InvalidOperationException>(() => services.ReplaceWorkflowRetryHandler());
    }

    private (WorkflowInstance Workflow, ExecutionPointer Pointer) Handle(Exception exception)
    {
        var workflow = new WorkflowInstance { Id = Guid.NewGuid().ToString(), Status = WorkflowStatus.Runnable };
        var pointer = new ExecutionPointer { Id = Guid.NewGuid().ToString(), Active = true };
        var step = new WorkflowStep<InlineStepBody> { RetryInterval = TimeSpan.FromSeconds(1) };

        _handler.Handle(workflow, new WorkflowDefinition(), pointer, step, exception, new Queue<ExecutionPointer>());

        return (workflow, pointer);
    }

    private static List<IWorkflowErrorHandler> ResolveHandlers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWorkflow();
        services.ReplaceWorkflowRetryHandler();

        using var provider = services.BuildServiceProvider();
        return provider.GetServices<IWorkflowErrorHandler>().ToList();
    }
}
