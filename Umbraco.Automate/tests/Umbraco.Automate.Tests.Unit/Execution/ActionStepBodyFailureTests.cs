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
using Umbraco.Cms.Core.Events;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution;

/// <summary>
/// How <see cref="ActionStepBody"/> surfaces a failed step to WorkflowCore. Under Retry, a failure
/// that retrying cannot fix — a terminal category, or an exhausted retry budget — is thrown as a
/// <see cref="NonRetryableStepFailureException"/> so <see cref="AutomateRetryHandler"/> terminates
/// the run, rather than returned as <see cref="ExecutionResult.Next"/>, which would follow every
/// unnamed edge out of the failed step.
/// </summary>
public class ActionStepBodyFailureTests
{
    private const int MaxRetries = 2;

    private readonly Mock<IAction> _action = new();
    private readonly Mock<IAutomationRunRepository> _runRepository = new();

    public ActionStepBodyFailureTests()
    {
        _action.Setup(a => a.Alias).Returns("test.failing");
    }

    // --- Terminal category under Retry ---

    [Fact]
    public async Task TerminalFailureUnderRetry_ThrowsANonRetryableFailure()
    {
        await Should.ThrowAsync<NonRetryableStepFailureException>(
            () => RunFailingStepAsync(StepErrorBehavior.Retry, StepRunErrorCategory.Validation, CreateContext()));
    }

    [Fact]
    public async Task TerminalFailureUnderRetry_CarriesTheStepErrorAsTheInnerException()
    {
        var thrown = await Should.ThrowAsync<NonRetryableStepFailureException>(
            () => RunFailingStepAsync(StepErrorBehavior.Retry, StepRunErrorCategory.Validation, CreateContext()));

        thrown.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task TerminalFailureUnderRetry_RecordsTheErrorCategoryOnTheStepRun()
    {
        await Should.ThrowAsync<NonRetryableStepFailureException>(
            () => RunFailingStepAsync(StepErrorBehavior.Retry, StepRunErrorCategory.Validation, CreateContext()));

        _runRepository.Verify(r => r.UpdateStepRunAsync(
            It.Is<StepRun>(s => s.Status == StepRunStatus.Failed && s.ErrorCategory == StepRunErrorCategory.Validation),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task TerminalSetupFailureUnderRetry_ThrowsANonRetryableFailure()
    {
        // Misconfigured settings throw before the pipeline runs; DefaultStepErrorClassifier
        // classifies an ArgumentException as a (terminal) configuration error.
        _action.Setup(a => a.SettingsType).Returns(typeof(object));
        _action.Setup(a => a.ResolveSettings(It.IsAny<Dictionary<string, object?>>()))
            .Throws(new ArgumentException("bad setting"));
        var stepConfig = StepConfig(StepErrorBehavior.Retry);
        stepConfig.Settings["value"] = "x";

        await Should.ThrowAsync<NonRetryableStepFailureException>(() => CreateBody(stepConfig).RunAsync(CreateContext()));
    }

    [Fact]
    public async Task ApprovalResumedWithoutADecisionUnderRetry_ThrowsANonRetryableFailure()
    {
        var stepConfig = StepConfig(StepErrorBehavior.Retry);
        var context = CreateContext();
        var data = (AutomationWorkflowData)context.Workflow.Data;
        context.ExecutionPointer.EventPublished = true;
        context.ExecutionPointer.EventData = "not a decision";
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
                StepRuns = [new StepRun { Id = Guid.NewGuid(), RunId = data.RunId, ActionAlias = "test.failing", StepId = stepConfig.Id, Status = StepRunStatus.WaitingForInput }],
            });

        await Should.ThrowAsync<NonRetryableStepFailureException>(() => CreateBody(stepConfig).RunAsync(context));
    }

    // --- Retry budget under Retry ---

    [Fact]
    public async Task ExhaustedRetryBudgetUnderRetry_ThrowsANonRetryableFailure()
    {
        await Should.ThrowAsync<NonRetryableStepFailureException>(
            () => RunFailingStepAsync(StepErrorBehavior.Retry, StepRunErrorCategory.ServiceUnavailable, CreateContext(retryCount: MaxRetries)));
    }

    [Fact]
    public async Task TransientFailureWithinBudget_ThrowsTheStepErrorSoWorkflowCoreRetries()
    {
        await Should.ThrowAsync<HttpRequestException>(
            () => RunFailingStepAsync(StepErrorBehavior.Retry, StepRunErrorCategory.ServiceUnavailable, CreateContext(retryCount: MaxRetries - 1)));
    }

    // --- Terminate, Suspend and Compensate: the step's own error, so WorkflowCore's handler applies ---

    [Theory]
    [InlineData(StepErrorBehavior.Terminate, StepRunErrorCategory.Validation)]
    [InlineData(StepErrorBehavior.Terminate, StepRunErrorCategory.ServiceUnavailable)]
    [InlineData(StepErrorBehavior.Suspend, StepRunErrorCategory.Validation)]
    [InlineData(StepErrorBehavior.Suspend, StepRunErrorCategory.ServiceUnavailable)]
    [InlineData(StepErrorBehavior.Compensate, StepRunErrorCategory.Validation)]
    [InlineData(StepErrorBehavior.Compensate, StepRunErrorCategory.ServiceUnavailable)]
    public async Task NonRetryBehaviours_ThrowTheStepErrorSoWorkflowCoreAppliesTheConfiguredHandler(
        StepErrorBehavior behavior, StepRunErrorCategory category)
    {
        await Should.ThrowAsync<HttpRequestException>(
            () => RunFailingStepAsync(behavior, category, CreateContext(retryCount: MaxRetries)));
    }

    private Task<ExecutionResult> RunFailingStepAsync(
        StepErrorBehavior behavior,
        StepRunErrorCategory category,
        IStepExecutionContext context)
    {
        _action.Setup(a => a.ExecuteAsync(It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActionResult.Failed(new HttpRequestException("boom"), category));

        return CreateBody(StepConfig(behavior)).RunAsync(context);
    }

    private static StepConfiguration StepConfig(StepErrorBehavior behavior) => new()
    {
        Id = Guid.NewGuid(),
        ActionAlias = "test.failing",
        Name = "Failing",
        Alias = "failing",
        Settings = [],
        ErrorBehavior = behavior,
        MaxRetries = MaxRetries,
    };

    private ActionStepBody CreateBody(StepConfiguration stepConfig)
    {
        var evaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        var hydrationCache = new StepOutputHydrationCache(_runRepository.Object);

        var meterFactory = new Mock<IMeterFactory>();
        meterFactory.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts.Name));

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
            Options.Create(new ExecutionOptions()),
            new AutomateMetrics(meterFactory.Object),
            Mock.Of<IEventAggregator>(),
            Mock.Of<ILogger<ActionStepBody>>());
    }

    private static IStepExecutionContext CreateContext(int retryCount = 0) => new StepExecutionContext
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
            RetryCount = retryCount,
        },
        CancellationToken = CancellationToken.None,
    };
}
