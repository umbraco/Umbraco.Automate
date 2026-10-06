using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
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
/// An approval step resumed when no step run is waiting for input — the crash window between saving
/// the decision on the step run and persisting the workflow. The step routes by the decision the step
/// run records instead of following only its unnamed lines, and fails when there is nothing recorded.
/// </summary>
public class ActionStepBodyApprovalRecoveryTests
{
    private readonly Mock<IAction> _action = new();
    private readonly Mock<IAutomationRunRepository> _runRepository = new();
    private readonly StepConfiguration _stepConfig = new()
    {
        Id = Guid.NewGuid(),
        ActionAlias = RequestApprovalAction.ApprovalActionAlias,
        Name = "Approval",
        Alias = "approval",
        Settings = [],
        ErrorBehavior = StepErrorBehavior.Retry,
    };

    public ActionStepBodyApprovalRecoveryTests()
    {
        _action.Setup(a => a.Alias).Returns(RequestApprovalAction.ApprovalActionAlias);
    }

    // --- A recorded decision routes the step ---

    [Fact]
    public async Task RecordedApproval_FollowsTheApprovedOutcome()
    {
        var context = ResumedContext();
        RecordStepRuns(context, StepRunFor(context, StepRunStatus.Completed));

        var result = await CreateBody().RunAsync(context);

        result.OutcomeValue.ShouldBe(RequestApprovalAction.ApprovedOutcome);
    }

    [Fact]
    public async Task RecordedRejection_FollowsTheRejectedOutcome()
    {
        var context = ResumedContext();
        RecordStepRuns(context, StepRunFor(context, StepRunStatus.Rejected));

        var result = await CreateBody().RunAsync(context);

        result.OutcomeValue.ShouldBe(RequestApprovalAction.RejectedOutcome);
    }

    [Fact]
    public async Task RecordedDecision_DoesNotAddAStepRun()
    {
        var context = ResumedContext();
        RecordStepRuns(context, StepRunFor(context, StepRunStatus.Completed));

        await CreateBody().RunAsync(context);

        _runRepository.Verify(r => r.AddStepRunAsync(It.IsAny<StepRun>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordedDecision_DoesNotSaveTheStepRunAgain()
    {
        var context = ResumedContext();
        RecordStepRuns(context, StepRunFor(context, StepRunStatus.Completed));

        await CreateBody().RunAsync(context);

        _runRepository.Verify(r => r.UpdateStepRunAsync(It.IsAny<StepRun>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordedDecision_DoesNotSaveTheRunAgain()
    {
        var context = ResumedContext();
        RecordStepRuns(context, StepRunFor(context, StepRunStatus.Completed));

        await CreateBody().RunAsync(context);

        _runRepository.Verify(r => r.SaveAsync(It.IsAny<AutomationRun>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordedDecision_PutsTheRecordedOutputBackForLaterSteps()
    {
        var context = ResumedContext();
        RecordStepRuns(context, StepRunFor(context, StepRunStatus.Completed));

        await CreateBody().RunAsync(context);

        ((AutomationWorkflowData)context.Workflow.Data).StepOutputs.ShouldContainKey(_stepConfig.Id);
    }

    [Fact]
    public async Task RecordedDecisionFromAnEarlierIteration_IsNotReused()
    {
        // The latest step run for the step is the one the event belongs to. An earlier loop
        // iteration's decision says nothing about this one.
        var context = ResumedContext();
        var earlier = StepRunFor(context, StepRunStatus.Completed);
        earlier.StartedUtc = DateTime.UtcNow.AddMinutes(-5);
        RecordStepRuns(context, earlier, StepRunFor(context, StepRunStatus.Failed));

        await Should.ThrowAsync<NonRetryableStepFailureException>(() => CreateBody().RunAsync(context));
    }

    // --- Nothing waiting and nothing recorded: a failure, never Next() ---

    [Fact]
    public async Task NoWaitingRunAndNoDecisionUnderRetry_ThrowsANonRetryableFailure()
    {
        var context = ResumedContext();
        RecordStepRuns(context);

        await Should.ThrowAsync<NonRetryableStepFailureException>(() => CreateBody().RunAsync(context));
    }

    [Fact]
    public async Task NoWaitingRunAndNoDecisionUnderSuspend_ThrowsTheStepErrorSoWorkflowCoreSuspends()
    {
        _stepConfig.ErrorBehavior = StepErrorBehavior.Suspend;
        var context = ResumedContext();
        RecordStepRuns(context);

        await Should.ThrowAsync<InvalidOperationException>(() => CreateBody().RunAsync(context));
    }

    [Fact]
    public async Task NoWaitingRunAndNoDecision_DropsTheEventSoAResumedStepAsksAgain()
    {
        _stepConfig.ErrorBehavior = StepErrorBehavior.Suspend;
        var context = ResumedContext();
        RecordStepRuns(context);

        await Should.ThrowAsync<InvalidOperationException>(() => CreateBody().RunAsync(context));

        context.ExecutionPointer.EventPublished.ShouldBeFalse();
    }

    private StepRun StepRunFor(IStepExecutionContext context, StepRunStatus status) => new()
    {
        Id = Guid.NewGuid(),
        RunId = ((AutomationWorkflowData)context.Workflow.Data).RunId,
        StepId = _stepConfig.Id,
        ActionAlias = RequestApprovalAction.ApprovalActionAlias,
        Status = status,
        StartedUtc = DateTime.UtcNow,
        OutputData = status is StepRunStatus.Completed or StepRunStatus.Rejected
            ? $$"""{"approved":{{(status == StepRunStatus.Completed ? "true" : "false")}},"outcome":"{{(status == StepRunStatus.Completed ? "Approved" : "Rejected")}}"}"""
            : null,
    };

    private void RecordStepRuns(IStepExecutionContext context, params StepRun[] stepRuns)
    {
        var runId = ((AutomationWorkflowData)context.Workflow.Data).RunId;
        _runRepository.Setup(r => r.GetAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRun
            {
                Id = runId,
                AutomationId = Guid.NewGuid(),
                AutomationVersion = 1,
                WorkspaceId = Guid.NewGuid(),
                ServiceAccountKey = Guid.NewGuid(),
                InitiatedBy = "test",
                Status = AutomationRunStatus.Running,
                StepRuns = [.. stepRuns],
            });
    }

    private ActionStepBody CreateBody()
    {
        var evaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        var hydrationCache = new StepOutputHydrationCache(_runRepository.Object);

        var meterFactory = new Mock<IMeterFactory>();
        meterFactory.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts.Name));

        return new ActionStepBody(
            _stepConfig,
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

    /// <summary>A pointer woken by a valid approval decision, as WorkflowCore re-runs it after a crash.</summary>
    private static IStepExecutionContext ResumedContext() => new StepExecutionContext
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
            EventPublished = true,
            EventData = new ApprovalDecision
            {
                Outcome = ApprovalOutcome.Approved,
                ApprovedByUserKey = Guid.NewGuid(),
                DecisionUtc = DateTime.UtcNow,
            },
        },
        CancellationToken = CancellationToken.None,
    };
}
