using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Cms.Core.Events;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution;

/// <summary>
/// How <see cref="ActionStepBody"/> routes an action that declares outcomes when the result does
/// not come straight back from the action: a step resuming after a sleep, and a step type whose
/// outcome list does not return within the step timeout.
/// </summary>
public class ActionStepBodyOutcomeTests
{
    private readonly Mock<IAction> _action = new();
    private readonly Mock<IAutomationRunRepository> _runRepository = new();

    public ActionStepBodyOutcomeTests()
    {
        _action.Setup(a => a.Alias).Returns("test.outcomes");
    }

    // --- Resuming after a sleep ---

    [Fact]
    public async Task SleepResume_WithDeclaredDefault_FollowsTheDefaultExit()
    {
        DeclareOutcomes(new StepOutcome("yes", "Yes"), new StepOutcome("no", "No") { IsDefault = true });

        var result = await ResumeAfterSleepAsync(StepErrorBehavior.Retry);

        result.OutcomeValue.ShouldBe("no");
    }

    [Fact]
    public async Task SleepResume_WithDeclaredDefault_RecordsTheExitTaken()
    {
        DeclareOutcomes(new StepOutcome("yes", "Yes"), new StepOutcome("no", "No") { IsDefault = true });

        await ResumeAfterSleepAsync(StepErrorBehavior.Retry);

        _runRepository.Verify(r => r.UpdateStepRunAsync(
            It.Is<StepRun>(s => s.Status == StepRunStatus.Completed && s.BranchOutcome == "no"),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task SleepResume_WithNoDeclaredOutcomes_ProceedsWithoutAnOutcome()
    {
        DeclareOutcomes();

        var result = await ResumeAfterSleepAsync(StepErrorBehavior.Retry);

        result.OutcomeValue.ShouldBeNull();
    }

    [Fact]
    public async Task SleepResume_WithOutcomesButNoDefault_FailsTheStepWithoutRetrying()
    {
        DeclareOutcomes(new StepOutcome("yes", "Yes"), new StepOutcome("no", "No"));

        await Should.ThrowAsync<NonRetryableStepFailureException>(() => ResumeAfterSleepAsync(StepErrorBehavior.Retry));

        _runRepository.Verify(r => r.UpdateStepRunAsync(
            It.Is<StepRun>(s => s.Status == StepRunStatus.Failed && s.ErrorCategory == StepRunErrorCategory.Validation),
            It.IsAny<CancellationToken>()));
    }

    // --- Outcome list bounded by the step timeout ---

    [Fact]
    public async Task OutcomeListThatOutlastsTheStepTimeout_FailsTheStepWithoutRetrying()
    {
        _action.Setup(a => a.HasDynamicOutcomes).Returns(true);
        _action.Setup(a => a.GetOutcomesAsync(It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>()))
            .Returns(async (Dictionary<string, object?>? _, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return (IReadOnlyList<StepOutcome>)[];
            });
        _action.Setup(a => a.ExecuteAsync(It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActionResult.Success());

        var body = CreateBody(StepConfig(StepErrorBehavior.Retry), TimeSpan.FromMilliseconds(200));

        // The guard turns a regression (an outcome list the step timeout doesn't bound) into a
        // failure rather than a test run that never finishes.
        await Should.ThrowAsync<NonRetryableStepFailureException>(
            () => body.RunAsync(CreateContext()).WaitAsync(TimeSpan.FromSeconds(10)));

        _runRepository.Verify(r => r.UpdateStepRunAsync(
            It.Is<StepRun>(s => s.Status == StepRunStatus.Failed && s.Error!.Contains("before the step timed out")),
            It.IsAny<CancellationToken>()));
    }

    private void DeclareOutcomes(params StepOutcome[] outcomes)
    {
        _action.Setup(a => a.HasDynamicOutcomes).Returns(false);
        _action.Setup(a => a.GetOutcomes()).Returns(outcomes);
    }

    private Task<ExecutionResult> ResumeAfterSleepAsync(StepErrorBehavior behavior)
    {
        var stepConfig = StepConfig(behavior);
        var context = CreateContext();
        var data = (AutomationWorkflowData)context.Workflow.Data;
        var stepRunId = Guid.NewGuid();

        _runRepository.Setup(r => r.GetAsync(data.RunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRun
            {
                Id = data.RunId,
                AutomationId = Guid.NewGuid(),
                AutomationVersion = 1,
                WorkspaceId = Guid.NewGuid(),
                ServiceAccountKey = Guid.NewGuid(),
                InitiatedBy = "test",
                Status = AutomationRunStatus.Running,
                StepRuns =
                [
                    new StepRun
                    {
                        Id = stepRunId,
                        RunId = data.RunId,
                        ActionAlias = "test.outcomes",
                        StepId = stepConfig.Id,
                        Status = StepRunStatus.Sleeping,
                        StartedUtc = DateTime.UtcNow,
                    },
                ],
            });

        context.PersistenceData = new SleepPersistenceData { StepRunId = stepRunId, RunId = data.RunId, StepId = stepConfig.Id };

        return CreateBody(stepConfig).RunAsync(context);
    }

    private static StepConfiguration StepConfig(StepErrorBehavior behavior) => new()
    {
        Id = Guid.NewGuid(),
        ActionAlias = "test.outcomes",
        Name = "Outcomes",
        Alias = "outcomes",
        Settings = [],
        ErrorBehavior = behavior,
        MaxRetries = 2,
    };

    private ActionStepBody CreateBody(StepConfiguration stepConfig, TimeSpan? stepTimeout = null)
    {
        var evaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        var hydrationCache = new StepOutputHydrationCache(_runRepository.Object);

        var meterFactory = new Mock<IMeterFactory>();
        meterFactory.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts.Name));

        var executionOptions = new ExecutionOptions();
        if (stepTimeout is { } timeout)
        {
            executionOptions.DefaultTimeout = timeout;
        }

        return new ActionStepBody(
            stepConfig,
            _action.Object,
            new ActionMiddlewarePipeline(new ActionMiddlewareCollection(Array.Empty<IActionMiddleware>)),
            evaluator,
            new ForEachCollectionCache(evaluator, hydrationCache),
            hydrationCache,
            new SettingsBindingResolver(evaluator),
            _runRepository.Object,
            Mock.Of<IConnectionService>(),
            new DefaultStepErrorClassifier(),
            Options.Create(executionOptions),
            new AutomateMetrics(meterFactory.Object),
            Mock.Of<IEventAggregator>(),
            Mock.Of<ILogger<ActionStepBody>>());
    }

    private static IStepExecutionContext CreateContext() => new StepExecutionContext
    {
        Workflow = new WorkflowInstance
        {
            Id = Guid.NewGuid().ToString(),
            Data = new AutomationWorkflowData { RunId = Guid.NewGuid() },
            Status = WorkflowStatus.Runnable,
        },
        ExecutionPointer = new ExecutionPointer
        {
            Id = Guid.NewGuid().ToString(),
            Active = true,
            Status = PointerStatus.Running,
        },
        CancellationToken = CancellationToken.None,
    };
}
