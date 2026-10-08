using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Dispatch.Authorization;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Extensions;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Persistence.Workflows;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Tests.Common;
using Umbraco.Automate.Tests.Common.Fixtures;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Sync;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// End-to-end checks that a step failure under the Retry error behaviour stops the run once
/// retrying cannot help — a terminal error category, or an exhausted retry budget — instead of
/// following the failed step's edges. An unlabelled edge (null outcome value) matches any outcome
/// in WorkflowCore, so a step that "skips past" its failure still fires the next step; these
/// tests pin that it no longer does, on a real WorkflowCore host with <see cref="RunFinalizer"/>
/// and <see cref="AutomateRetryHandler"/> wired in as production wires them.
/// </summary>
[Collection("WorkflowHost")]
public class TerminalStepFailureTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private IWorkflowHost _workflowHost = null!;
    private EfCoreTestFixture _fixture = null!;
    private TriggerEventHandler _handler = null!;
    private IAutomationRunRepository _runRepository = null!;
    private IAutomationRunService _runService = null!;
    private IPersistenceProvider _persistence = null!;
    private Mock<IAutomationService> _automationServiceMock = null!;

    public async Task InitializeAsync()
    {
        _fixture = new EfCoreTestFixture();
        var dbContextFactory = new TestDbContextFactory(_fixture.CreateContext);
        var configuration = new ConfigurationBuilder().Build();

        var modelResolver = new EditableModelResolver(new ConfigurationReferenceResolver(configuration));

        var actions = new ActionCollection(() =>
        {
            var deps = new ActionInfrastructure(modelResolver);
            return new IAction[]
            {
                new LogMessageAction(deps, LoggerFactory.Create(b => b.AddDebug()).CreateLogger<LogMessageAction>()),
                new TerminalFailureAction(deps),
                new TransientFailureAction(deps),
                new RequestApprovalAction(deps),
            };
        });

        var triggers = new TriggerCollection(() =>
        {
            var deps = new TriggerInfrastructure(modelResolver);
            return new ITrigger[] { new ManualTrigger(deps) };
        });

        var controlFlow = new ControlFlowCollection(() =>
        {
            var deps = new ControlFlowInfrastructure(modelResolver);
            return new IControlFlow[] { new ForEachControlFlow(deps), new ParallelControlFlow(deps) };
        });

        var middlewareCollection = new ActionMiddlewareCollection(Array.Empty<IActionMiddleware>);

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddWorkflow();
        services.ReplaceWorkflowRetryHandler();

        // The run row only reaches its final status through RunFinalizer, which the production
        // persistence provider calls on every persist. Register it exactly as the Persistence
        // composer does — WorkflowCore's consumers inject the sub-interfaces directly — so the
        // tests can assert on AutomationRun.Status as production sees it.
        services.AddSingleton(sp => new EFCoreWorkflowPersistenceProvider(
            dbContextFactory,
            sp.GetRequiredService<RunFinalizer>(),
            sp.GetRequiredService<ILogger<EFCoreWorkflowPersistenceProvider>>()));
        services.AddSingleton<IPersistenceProvider>(sp => sp.GetRequiredService<EFCoreWorkflowPersistenceProvider>());
        services.AddSingleton<IWorkflowRepository>(sp => sp.GetRequiredService<EFCoreWorkflowPersistenceProvider>());
        services.AddSingleton<ISubscriptionRepository>(sp => sp.GetRequiredService<EFCoreWorkflowPersistenceProvider>());
        services.AddSingleton<IEventRepository>(sp => sp.GetRequiredService<EFCoreWorkflowPersistenceProvider>());

        services.AddMemoryCache();
        services.AddWorkflowStepMiddleware<RunCancellationStepMiddleware>();

        _runRepository = new EFCoreAutomationRunRepository(dbContextFactory);
        services.AddSingleton(_runRepository);

        var scopeProvider = new Mock<ICoreScopeProvider> { DefaultValue = DefaultValue.Mock };
        services.AddSingleton(scopeProvider.Object);
        services.AddSingleton(Mock.Of<IEventMessagesFactory>(f => f.Get() == new EventMessages()));
        services.AddSingleton<RunFinalizer>();

        services.AddSingleton(actions);
        services.AddSingleton(triggers);
        services.AddSingleton(controlFlow);
        services.AddSingleton(middlewareCollection);
        services.AddSingleton(new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>)));
        services.AddSingleton<ForEachCollectionCache>();
        services.AddSingleton<StepOutputHydrationCache>();
        services.AddSingleton<SettingsBindingResolver>();
        services.AddSingleton<ConditionEvaluator>();
        services.AddSingleton<ActionMiddlewarePipeline>();
        services.AddMetrics();
        services.AddSingleton<AutomateMetrics>();

        var workspace = new WorkspaceBuilder().WithName("Terminal Failure Workspace").Build();
        var workspaceService = new Mock<IWorkspaceService>();
        workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);
        services.AddSingleton(workspaceService.Object);

        var serviceAccountResolver = new Mock<IWorkspaceServiceAccountResolver>();
        serviceAccountResolver.Setup(r => r.GetServiceAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IUser>(u => u.AllowedSections == new[] { "content", "media", "members", "users" }));
        services.AddSingleton(serviceAccountResolver.Object);
        services.AddSingleton<ISectionAccessChecker, SectionAccessChecker>();

        services.AddSingleton(new TriggerDispatchAuthorizerCollection(Array.Empty<ITriggerDispatchAuthorizer>));
        services.AddSingleton(Mock.Of<IConnectionService>());

        services.Configure<RateLimitingOptions>(o => o.Enabled = false);
        services.AddSingleton<IRateLimitService, RateLimitService>();

        services.AddSingleton<IStepErrorClassifier, DefaultStepErrorClassifier>();
        services.AddSingleton<IWorkflowCompiler, WorkflowCompiler>();
        services.AddSingleton<ICircuitBreakerService, StubCircuitBreakerService>();
        services.AddSingleton<IEventAggregator>(Mock.Of<IEventAggregator>());
        services.AddSingleton<IAutomationExecutor, AutomationExecutor>();

        _provider = services.BuildServiceProvider();

        _workflowHost = _provider.GetRequiredService<IWorkflowHost>();
        _persistence = _provider.GetRequiredService<IPersistenceProvider>();
        await _workflowHost.StartAsync(CancellationToken.None);

        _runService = new AutomationRunService(
            _runRepository,
            Mock.Of<IRunDataSanitizer>(),
            _workflowHost,
            _provider.GetRequiredService<IEventAggregator>(),
            _provider.GetRequiredService<ILogger<AutomationRunService>>());

        _automationServiceMock = new Mock<IAutomationService>();

        var nodeEligibility = new Mock<IExecutionNodeEligibility>();
        nodeEligibility.Setup(e => e.CanExecuteWorkflows()).Returns(true);

        _handler = new TriggerEventHandler(
            _automationServiceMock.Object,
            Mock.Of<IEntityVersionService>(),
            _provider.GetRequiredService<IAutomationExecutor>(),
            nodeEligibility.Object,
            triggers,
            _provider.GetRequiredService<IWorkspaceServiceAccountResolver>(),
            _provider.GetRequiredService<ISectionAccessChecker>(),
            _provider.GetRequiredService<TriggerDispatchAuthorizerCollection>(),
            _provider.GetRequiredService<IOptionsMonitor<ExecutionOptions>>(),
            _provider.GetRequiredService<ILogger<TriggerEventHandler>>());
    }

    // --- Terminal category under Retry ---

    [Fact]
    public async Task TerminalFailureUnderRetry_DoesNotRunTheStepBehindAnUnnamedLine()
    {
        var (run, _, after) = await RunLinearAsync(TerminalFailureAction.StepAlias, StepErrorBehavior.Retry);

        run.StepRuns.ShouldNotContain(s => s.StepId == after.Id);
    }

    [Fact]
    public async Task TerminalFailureUnderRetry_EndsTheRunFailed()
    {
        var (run, _, _) = await RunLinearAsync(TerminalFailureAction.StepAlias, StepErrorBehavior.Retry);

        run.Status.ShouldBe(AutomationRunStatus.Failed);
    }

    [Fact]
    public async Task TerminalFailureUnderRetry_KeepsTheErrorCategoryOnTheStepRun()
    {
        var (run, failing, _) = await RunLinearAsync(TerminalFailureAction.StepAlias, StepErrorBehavior.Retry);

        run.StepRuns.Single(s => s.StepId == failing.Id).ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task TerminalFailureUnderRetry_KeepsTheErrorOnTheStepRun()
    {
        var (run, failing, _) = await RunLinearAsync(TerminalFailureAction.StepAlias, StepErrorBehavior.Retry);

        run.StepRuns.Single(s => s.StepId == failing.Id).Error.ShouldBe(TerminalFailureAction.Message);
    }

    [Fact]
    public async Task TerminalFailureUnderRetry_ReportsTheStepErrorOnTheRun()
    {
        var (run, _, _) = await RunLinearAsync(TerminalFailureAction.StepAlias, StepErrorBehavior.Retry);

        run.Error.ShouldBe(TerminalFailureAction.Message);
    }

    [Fact]
    public async Task TerminalFailureUnderRetry_IsNotRetried()
    {
        var (run, failing, _) = await RunLinearAsync(TerminalFailureAction.StepAlias, StepErrorBehavior.Retry);

        run.StepRuns.Count(s => s.StepId == failing.Id).ShouldBe(1);
    }

    // --- Exhausted retry budget under Retry ---

    [Fact]
    public async Task ExhaustedRetryBudget_DoesNotRunTheStepBehindAnUnnamedLine()
    {
        var (run, _, after) = await RunLinearAsync(TransientFailureAction.StepAlias, StepErrorBehavior.Retry, maxRetries: 2);

        run.StepRuns.ShouldNotContain(s => s.StepId == after.Id);
    }

    [Fact]
    public async Task ExhaustedRetryBudget_EndsTheRunFailed()
    {
        var (run, _, _) = await RunLinearAsync(TransientFailureAction.StepAlias, StepErrorBehavior.Retry, maxRetries: 2);

        run.Status.ShouldBe(AutomationRunStatus.Failed);
    }

    [Fact]
    public async Task TransientFailureUnderRetry_IsStillRetriedUpToTheBudget()
    {
        var (run, failing, _) = await RunLinearAsync(TransientFailureAction.StepAlias, StepErrorBehavior.Retry, maxRetries: 2);

        // The first attempt plus two retries.
        run.StepRuns.Count(s => s.StepId == failing.Id).ShouldBe(3);
    }

    // --- Terminate and Suspend are unchanged ---

    [Fact]
    public async Task TerminalFailureUnderTerminate_DoesNotRunTheNextStep()
    {
        var (run, _, after) = await RunLinearAsync(TerminalFailureAction.StepAlias, StepErrorBehavior.Terminate);

        run.StepRuns.ShouldNotContain(s => s.StepId == after.Id);
    }

    [Fact]
    public async Task TerminalFailureUnderTerminate_EndsTheRunFailed()
    {
        var (run, _, _) = await RunLinearAsync(TerminalFailureAction.StepAlias, StepErrorBehavior.Terminate);

        run.Status.ShouldBe(AutomationRunStatus.Failed);
    }

    [Fact]
    public async Task TerminalFailureUnderSuspend_SuspendsTheRun()
    {
        var (run, _, _) = await RunLinearAsync(
            TerminalFailureAction.StepAlias, StepErrorBehavior.Suspend, waitFor: AutomationRunStatus.Suspended);

        run.Status.ShouldBe(AutomationRunStatus.Suspended);
    }

    // --- Containers: a failure inside a body stops the whole run, not just the branch ---

    [Fact]
    public async Task TerminalFailureInSequentialForEachBody_DoesNotRunLaterIterations()
    {
        var (run, failing, _) = await RunForEachAsync(runParallel: false);

        run.StepRuns.Count(s => s.StepId == failing.Id).ShouldBe(1);
    }

    [Fact]
    public async Task TerminalFailureInParallelForEachBody_DoesNotRunSiblingIterations()
    {
        var (run, failing, _) = await RunForEachAsync(runParallel: true);

        run.StepRuns.Count(s => s.StepId == failing.Id).ShouldBe(1);
    }

    [Fact]
    public async Task TerminalFailureInForEachBody_DoesNotRunTheDoneStep()
    {
        var (run, _, after) = await RunForEachAsync(runParallel: false);

        run.StepRuns.ShouldNotContain(s => s.StepId == after.Id);
    }

    [Fact]
    public async Task TerminalFailureInForEachBody_EndsTheRunFailed()
    {
        var (run, _, _) = await RunForEachAsync(runParallel: false);

        run.Status.ShouldBe(AutomationRunStatus.Failed);
    }

    [Fact]
    public async Task TerminalFailureInParallelBranch_DoesNotRunTheSiblingBranch()
    {
        // Both branches start in the same execution pass, in an order the test does not control,
        // so both are failing steps: whichever runs first stops the run, and the other must not run.
        var (run, _, _) = await RunParallelAsync();

        run.StepRuns.Count(s => s.ActionAlias == TerminalFailureAction.StepAlias).ShouldBe(1);
    }

    [Fact]
    public async Task TerminalFailureInParallelBranch_DoesNotRunTheDoneStep()
    {
        var (run, _, after) = await RunParallelAsync();

        run.StepRuns.ShouldNotContain(s => s.StepId == after.Id);
    }

    [Fact]
    public async Task TerminalFailureInParallelBranch_EndsTheRunFailed()
    {
        var (run, _, _) = await RunParallelAsync();

        run.Status.ShouldBe(AutomationRunStatus.Failed);
    }

    // --- Approval resumed with a payload that is not a decision ---

    [Fact]
    public async Task ApprovalResumedWithoutADecisionUnderRetry_EndsTheRunFailed()
    {
        var (run, _, _) = await RunApprovalWithoutDecisionAsync(StepErrorBehavior.Retry);

        (await WaitForRunStatusAsync(run.Id, AutomationRunStatus.Failed, TestTimeouts.WorkflowWait)).ShouldBe(AutomationRunStatus.Failed);
    }

    [Fact]
    public async Task ApprovalResumedWithoutADecisionUnderRetry_DoesNotRunTheStepBehindAnUnnamedLine()
    {
        var (run, _, after) = await RunApprovalWithoutDecisionAsync(StepErrorBehavior.Retry);
        await WaitForRunStatusAsync(run.Id, AutomationRunStatus.Failed, TestTimeouts.WorkflowWait);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        (await _runRepository.GetAsync(run.Id))!.StepRuns.ShouldNotContain(s => s.StepId == after.Id);
    }

    [Fact]
    public async Task ApprovalResumedWithoutADecisionUnderSuspend_SuspendsTheWorkflow()
    {
        var (run, _, _) = await RunApprovalWithoutDecisionAsync(StepErrorBehavior.Suspend);

        var instance = await WaitForWorkflowStatusAsync(run, WorkflowStatus.Suspended, TestTimeouts.WorkflowWait);
        instance.Status.ShouldBe(WorkflowStatus.Suspended);
    }

    [Fact]
    public async Task ApprovalResumedWithoutADecisionUnderSuspend_OnResume_AsksForADecisionAgain()
    {
        var (run, approval, _) = await SuspendAndResumeApprovalAsync();

        await WaitForStepRunCountAsync(run.Id, approval.Id, StepRunStatus.WaitingForInput, 1, TestTimeouts.WorkflowWait);
        (await _runRepository.GetAsync(run.Id))!.StepRuns
            .Count(s => s.StepId == approval.Id && s.Status == StepRunStatus.WaitingForInput)
            .ShouldBe(1);
    }

    [Fact]
    public async Task ApprovalResumedWithoutADecisionUnderSuspend_OnResume_DoesNotRunTheStepBehindAnUnnamedLine()
    {
        var (run, approval, after) = await SuspendAndResumeApprovalAsync();
        await WaitForStepRunCountAsync(run.Id, approval.Id, StepRunStatus.WaitingForInput, 1, TestTimeouts.WorkflowWait);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        (await _runRepository.GetAsync(run.Id))!.StepRuns.ShouldNotContain(s => s.StepId == after.Id);
    }

    [Fact]
    public async Task ApprovalResumedWithoutADecisionUnderSuspend_OnResumeAndApproval_RunsTheNextStep()
    {
        var (run, approval, after) = await SuspendAndResumeApprovalAsync();
        await WaitForStepRunCountAsync(run.Id, approval.Id, StepRunStatus.WaitingForInput, 1, TestTimeouts.WorkflowWait);

        await PublishApprovalEventAsync(run.Id, approval.Id, new ApprovalDecision
        {
            Outcome = ApprovalOutcome.Approved,
            ApprovedByUserKey = Guid.NewGuid(),
            DecisionUtc = DateTime.UtcNow,
        });

        await WaitForStepRunCountAsync(run.Id, after.Id, StepRunStatus.Completed, 1, TestTimeouts.WorkflowWait);
        (await _runRepository.GetAsync(run.Id))!.StepRuns.Count(s => s.StepId == after.Id).ShouldBe(1);
    }

    // --- Approval resumed with a decision, but no step run waiting and none recorded ---

    [Fact]
    public async Task ApprovalResumedWithNothingWaitingOrRecordedUnderRetry_EndsTheRunFailed()
    {
        var (run, _, _) = await ResumeApprovalWithNothingWaitingOrRecordedAsync(StepErrorBehavior.Retry);

        (await WaitForRunStatusAsync(run.Id, AutomationRunStatus.Failed, TestTimeouts.WorkflowWait)).ShouldBe(AutomationRunStatus.Failed);
    }

    [Fact]
    public async Task ApprovalResumedWithNothingWaitingOrRecordedUnderRetry_DoesNotRunTheStepBehindAnUnnamedLine()
    {
        var (run, _, after) = await ResumeApprovalWithNothingWaitingOrRecordedAsync(StepErrorBehavior.Retry);
        await WaitForRunStatusAsync(run.Id, AutomationRunStatus.Failed, TestTimeouts.WorkflowWait);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        (await _runRepository.GetAsync(run.Id))!.StepRuns.ShouldNotContain(s => s.StepId == after.Id);
    }

    [Fact]
    public async Task ApprovalResumedWithNothingWaitingOrRecordedUnderRetry_ShowsTheReasonInTheRunHistory()
    {
        var (run, approval, _) = await ResumeApprovalWithNothingWaitingOrRecordedAsync(StepErrorBehavior.Retry);
        await WaitForRunStatusAsync(run.Id, AutomationRunStatus.Failed, TestTimeouts.WorkflowWait);

        (await _runRepository.GetAsync(run.Id))!.StepRuns
            .ShouldContain(s => s.StepId == approval.Id
                && s.Status == StepRunStatus.Failed
                && s.Error != null && s.Error.Contains("none has been recorded"));
    }

    [Fact]
    public async Task ApprovalResumedWithNothingWaitingOrRecordedUnderSuspend_SuspendsTheWorkflow()
    {
        var (run, _, _) = await ResumeApprovalWithNothingWaitingOrRecordedAsync(StepErrorBehavior.Suspend);

        var instance = await WaitForWorkflowStatusAsync(run, WorkflowStatus.Suspended, TestTimeouts.WorkflowWait);
        instance.Status.ShouldBe(WorkflowStatus.Suspended);
    }

    [Fact]
    public async Task ApprovalResumedWithNothingWaitingOrRecordedUnderSuspend_OnResume_AsksForADecisionAgain()
    {
        var (run, approval, _) = await ResumeApprovalWithNothingWaitingOrRecordedAsync(StepErrorBehavior.Suspend);
        await WaitForWorkflowStatusAsync(run, WorkflowStatus.Suspended, TestTimeouts.WorkflowWait);
        await WaitForRunStatusAsync(run.Id, AutomationRunStatus.Suspended, TestTimeouts.WorkflowWait);

        (await _runService.ResumeRunAsync(run.Id)).ShouldBe(RunLifecycleResult.Success);

        await WaitForStepRunCountAsync(run.Id, approval.Id, StepRunStatus.WaitingForInput, 1, TestTimeouts.WorkflowWait);
        (await _runRepository.GetAsync(run.Id))!.StepRuns
            .Count(s => s.StepId == approval.Id && s.Status == StepRunStatus.WaitingForInput)
            .ShouldBe(1);
    }

    // --- Approval decided, but the process stopped before the decision was routed (#467) ---

    [Theory]
    [InlineData(ApprovalOutcome.Approved)]
    [InlineData(ApprovalOutcome.Rejected)]
    public async Task ApprovalDecidedButNotRoutedBeforeARestart_RunsTheLineTheDecisionChose(ApprovalOutcome outcome)
    {
        var (run, steps) = await RestartBeforeRoutingRecordedDecisionAsync(outcome);
        var chosen = outcome == ApprovalOutcome.Approved ? steps.Approved : steps.Rejected;

        await WaitForStepRunCountAsync(run.Id, chosen.Id, StepRunStatus.Completed, 1, TestTimeouts.WorkflowWait);
    }

    [Fact]
    public async Task ApprovalDecidedButNotRoutedBeforeARestart_CompletesTheRun()
    {
        var (run, _) = await RestartBeforeRoutingRecordedDecisionAsync(ApprovalOutcome.Approved);

        (await WaitForRunStatusAsync(run.Id, AutomationRunStatus.Completed, TestTimeouts.WorkflowWait)).ShouldBe(AutomationRunStatus.Completed);
    }

    /// <summary>ManualTrigger → failing step → (unnamed line) → log step.</summary>
    private async Task<(AutomationRun Run, StepConfiguration Failing, StepConfiguration After)> RunLinearAsync(
        string failingAlias,
        StepErrorBehavior behavior,
        int? maxRetries = null,
        AutomationRunStatus waitFor = AutomationRunStatus.Failed)
    {
        var failing = FailingStep(failingAlias, behavior, maxRetries);
        var after = LogStep("after", "after");

        var automation = new AutomationBuilder()
            .WithAlias($"test-terminal-linear-{Guid.NewGuid():N}")
            .WithName("test-terminal-linear")
            .WithManualTrigger()
            .AddStep(failing)
            .AddStep(after)
            .WithTriggerConnection(failing.Id)
            .WithConnection(failing.Id, after.Id)
            .Build();

        var run = await StartAndWaitAsync(automation, waitFor);
        return (run, failing, after);
    }

    /// <summary>ManualTrigger → ForEach(3 items) { failing step } → Done → log step.</summary>
    private async Task<(AutomationRun Run, StepConfiguration Failing, StepConfiguration After)> RunForEachAsync(bool runParallel)
    {
        var forEach = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.forEach",
            Name = "ForEach",
            Alias = "forEach",
            Settings = new Dictionary<string, object?>
            {
                ["collection"] = "a,b,c",
                ["runParallel"] = runParallel,
            },
        };
        var failing = FailingStep(TerminalFailureAction.StepAlias, StepErrorBehavior.Retry);
        var after = LogStep("afterLoop", "after-loop");

        var automation = new AutomationBuilder()
            .WithAlias($"test-terminal-foreach-{Guid.NewGuid():N}")
            .WithName("test-terminal-foreach")
            .WithManualTrigger()
            .AddStep(forEach)
            .AddStep(failing)
            .AddStep(after)
            .WithTriggerConnection(forEach.Id)
            .WithConnection(forEach.Id, failing.Id, sourceHandle: ContainerHandles.Body)
            .WithConnection(forEach.Id, after.Id, sourceHandle: ContainerHandles.Done)
            .Build();

        var run = await StartAndWaitAsync(automation, AutomationRunStatus.Failed);
        return (run, failing, after);
    }

    /// <summary>ManualTrigger → Parallel { failing step | failing step } → Done → log step.</summary>
    private async Task<(AutomationRun Run, StepConfiguration Sibling, StepConfiguration After)> RunParallelAsync()
    {
        var parallel = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.parallel",
            Name = "Parallel",
            Alias = "fanOut",
            Settings = new Dictionary<string, object?>(),
        };
        var failing = FailingStep(TerminalFailureAction.StepAlias, StepErrorBehavior.Retry);
        var sibling = FailingStep(TerminalFailureAction.StepAlias, StepErrorBehavior.Retry);
        sibling.Alias = "failingSibling";
        var after = LogStep("afterParallel", "after-parallel");

        var automation = new AutomationBuilder()
            .WithAlias($"test-terminal-parallel-{Guid.NewGuid():N}")
            .WithName("test-terminal-parallel")
            .WithManualTrigger()
            .AddStep(parallel)
            .AddStep(failing)
            .AddStep(sibling)
            .AddStep(after)
            .WithTriggerConnection(parallel.Id)
            .WithConnection(parallel.Id, failing.Id, sourceHandle: ContainerHandles.Body)
            .WithConnection(parallel.Id, sibling.Id, sourceHandle: ContainerHandles.Body)
            .WithConnection(parallel.Id, after.Id, sourceHandle: ContainerHandles.Done)
            .Build();

        var run = await StartAndWaitAsync(automation, AutomationRunStatus.Failed);
        return (run, sibling, after);
    }

    /// <summary>
    /// ManualTrigger → approval step → (unnamed line) → log step. Waits for the approval, then
    /// publishes an approval event whose payload is not a decision and waits for the step to fail.
    /// </summary>
    private async Task<(AutomationRun Run, StepConfiguration Approval, StepConfiguration After)> RunApprovalWithoutDecisionAsync(
        StepErrorBehavior behavior)
    {
        var approval = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = RequestApprovalAction.ApprovalActionAlias,
            Name = "Approval",
            Alias = "approval",
            Settings = new Dictionary<string, object?> { ["prompt"] = "Please approve" },
            ErrorBehavior = behavior,
        };
        var after = LogStep("afterApproval", "after-approval");

        var automation = new AutomationBuilder()
            .WithAlias($"test-terminal-approval-{Guid.NewGuid():N}")
            .WithName("test-terminal-approval")
            .WithManualTrigger()
            .AddStep(approval)
            .AddStep(after)
            .WithTriggerConnection(approval.Id)
            .WithConnection(approval.Id, after.Id)
            .Build();

        var runId = await TriggerAsync(automation);
        await WaitForStepRunCountAsync(runId, approval.Id, StepRunStatus.WaitingForInput, 1, TestTimeouts.WorkflowWait);

        await PublishApprovalEventAsync(runId, approval.Id, "not a decision");
        await WaitForStepRunCountAsync(runId, approval.Id, StepRunStatus.Failed, 1, TestTimeouts.WorkflowWait);

        return ((await _runRepository.GetAsync(runId))!, approval, after);
    }

    /// <summary>
    /// ManualTrigger → approval step → (unnamed line) → log step. Waits for the approval, then takes
    /// the step run out of WaitingForInput without recording a decision on it — state the step cannot
    /// explain — and publishes a valid decision, so the step resumes with nothing to route by.
    /// </summary>
    private async Task<(AutomationRun Run, StepConfiguration Approval, StepConfiguration After)> ResumeApprovalWithNothingWaitingOrRecordedAsync(
        StepErrorBehavior behavior)
    {
        var approval = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = RequestApprovalAction.ApprovalActionAlias,
            Name = "Approval",
            Alias = "approval",
            Settings = new Dictionary<string, object?> { ["prompt"] = "Please approve" },
            ErrorBehavior = behavior,
        };
        var after = LogStep("afterApproval", "after-approval");

        var automation = new AutomationBuilder()
            .WithAlias($"test-terminal-approval-unrecorded-{Guid.NewGuid():N}")
            .WithName("test-terminal-approval-unrecorded")
            .WithManualTrigger()
            .AddStep(approval)
            .AddStep(after)
            .WithTriggerConnection(approval.Id)
            .WithConnection(approval.Id, after.Id)
            .Build();

        var runId = await TriggerAsync(automation);
        await WaitForStepRunCountAsync(runId, approval.Id, StepRunStatus.WaitingForInput, 1, TestTimeouts.WorkflowWait);

        var waiting = (await _runRepository.GetAsync(runId))!.StepRuns
            .Single(s => s.StepId == approval.Id && s.Status == StepRunStatus.WaitingForInput);
        waiting.Status = StepRunStatus.Running;
        await _runRepository.UpdateStepRunAsync(waiting);

        await PublishApprovalEventAsync(runId, approval.Id, new ApprovalDecision
        {
            Outcome = ApprovalOutcome.Approved,
            ApprovedByUserKey = Guid.NewGuid(),
            DecisionUtc = DateTime.UtcNow,
        });

        return ((await _runRepository.GetAsync(runId))!, approval, after);
    }

    /// <summary>
    /// Runs <see cref="RunApprovalWithoutDecisionAsync"/> under Suspend, waits for the workflow to
    /// suspend, then resumes it the way the backoffice does.
    /// </summary>
    private async Task<(AutomationRun Run, StepConfiguration Approval, StepConfiguration After)> SuspendAndResumeApprovalAsync()
    {
        var (run, approval, after) = await RunApprovalWithoutDecisionAsync(StepErrorBehavior.Suspend);
        await WaitForWorkflowStatusAsync(run, WorkflowStatus.Suspended, TestTimeouts.WorkflowWait);
        await WaitForRunStatusAsync(run.Id, AutomationRunStatus.Suspended, TestTimeouts.WorkflowWait);

        var resumed = await _runService.ResumeRunAsync(run.Id);
        resumed.ShouldBe(RunLifecycleResult.Success);

        return (run, approval, after);
    }

    /// <summary>
    /// ManualTrigger → approval, with an approved line and a rejected line. Waits for the approval,
    /// stops the host, then leaves the database exactly as a process that stopped mid-resume leaves
    /// it: WorkflowCore's <c>EventConsumer.SeedSubscription</c> delivered the decision to the step's
    /// pointer, and the step saved the decision on its step run and moved the run back to Running,
    /// but the workflow was never persisted after the step. Then it runs stuck-run recovery and starts
    /// the host again, as a restart does.
    /// </summary>
    private async Task<(AutomationRun Run, RecordedDecisionSteps Steps)> RestartBeforeRoutingRecordedDecisionAsync(
        ApprovalOutcome outcome)
    {
        var approval = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = RequestApprovalAction.ApprovalActionAlias,
            Name = "Approval",
            Alias = "approval",
            Settings = new Dictionary<string, object?> { ["prompt"] = "Please approve" },
        };
        var steps = new RecordedDecisionSteps(
            approval,
            LogStep("approvedLog", "took-approved-path"),
            LogStep("rejectedLog", "took-rejected-path"));

        var automation = new AutomationBuilder()
            .WithAlias($"test-terminal-approval-restart-{Guid.NewGuid():N}")
            .WithName("test-terminal-approval-restart")
            .WithManualTrigger()
            .AddStep(approval)
            .AddStep(steps.Approved)
            .AddStep(steps.Rejected)
            .WithTriggerConnection(approval.Id)
            .WithConnection(approval.Id, steps.Approved.Id, RequestApprovalAction.ApprovedOutcome)
            .WithConnection(approval.Id, steps.Rejected.Id, RequestApprovalAction.RejectedOutcome)
            .Build();

        var runId = await TriggerAsync(automation);
        await WaitForStepRunCountAsync(runId, approval.Id, StepRunStatus.WaitingForInput, 1, TestTimeouts.WorkflowWait);
        await WaitForRunStatusAsync(runId, AutomationRunStatus.Suspended, TestTimeouts.WorkflowWait);

        await _workflowHost.StopAsync(CancellationToken.None);

        var decision = new ApprovalDecision
        {
            Outcome = outcome,
            ApprovedByUserKey = Guid.NewGuid(),
            DecisionUtc = DateTime.UtcNow,
        };
        var eventKey = $"{runId}:{approval.Id}";
        await PublishApprovalEventAsync(runId, approval.Id, decision);

        // What SeedSubscription persists before the step runs: the event on the pointer, the
        // subscription ended and the event processed.
        var run = (await _runRepository.GetAsync(runId))!;
        var instance = await _persistence.GetWorkflowInstance(run.WorkflowInstanceId!);
        foreach (var pointer in instance.ExecutionPointers.Where(p => p.EventKey == eventKey && !p.EventPublished && p.EndTime == null))
        {
            pointer.EventData = decision;
            pointer.EventPublished = true;
            pointer.Active = true;
        }

        instance.NextExecution = 0;
        await _persistence.PersistWorkflow(instance);

        foreach (var subscription in await _persistence.GetSubscriptions(RequestApprovalAction.ApprovalEventName, eventKey, DateTime.UtcNow))
        {
            await _persistence.TerminateSubscription(subscription.Id);
        }

        foreach (var eventId in await _persistence.GetEvents(RequestApprovalAction.ApprovalEventName, eventKey, DateTime.MinValue))
        {
            await _persistence.MarkEventProcessed(eventId);
        }

        // What the step saved before the process stopped (see ActionStepBody.HandleResumeAsync).
        run.Status = AutomationRunStatus.Running;
        await _runRepository.SaveAsync(run);

        var waiting = run.StepRuns.Single(s => s.StepId == approval.Id && s.Status == StepRunStatus.WaitingForInput);
        waiting.Status = outcome == ApprovalOutcome.Approved ? StepRunStatus.Completed : StepRunStatus.Rejected;
        waiting.CompletedUtc = DateTime.UtcNow;
        waiting.OutputData = JsonSerializer.Serialize(
            new ApprovalDecisionOutput
            {
                Approved = outcome == ApprovalOutcome.Approved,
                Outcome = outcome.ToString(),
                DecisionUtc = decision.DecisionUtc,
            },
            JsonOptions.Default);
        await _runRepository.UpdateStepRunAsync(waiting);

        var serverRoleAccessor = new Mock<IServerRoleAccessor>();
        serverRoleAccessor.Setup(r => r.CurrentServerRole).Returns(ServerRole.Single);
        var recovery = new EFCoreStuckRunRecovery(
            new TestDbContextFactory(_fixture.CreateContext),
            serverRoleAccessor.Object,
            Options.Create(new WorkflowLockOptions()),
            TimeProvider.System,
            NullLogger<EFCoreStuckRunRecovery>.Instance);
        await recovery.RecoverStuckRunsAsync(CancellationToken.None);

        await _workflowHost.StartAsync(CancellationToken.None);

        return (run, steps);
    }

    private sealed record RecordedDecisionSteps(
        StepConfiguration Approval,
        StepConfiguration Approved,
        StepConfiguration Rejected);

    /// <summary>Publishes the approval event as <c>SubmitApprovalController</c> does, with any payload.</summary>
    private Task PublishApprovalEventAsync(Guid runId, Guid stepId, object payload)
        => _workflowHost.PublishEvent(RequestApprovalAction.ApprovalEventName, $"{runId}:{stepId}", payload);

    private static StepConfiguration FailingStep(string actionAlias, StepErrorBehavior behavior, int? maxRetries = null) => new()
    {
        Id = Guid.NewGuid(),
        ActionAlias = actionAlias,
        Name = "Failing",
        Alias = "failing",
        Settings = new Dictionary<string, object?>(),
        ErrorBehavior = behavior,
        MaxRetries = maxRetries,
        RetryInterval = TimeSpan.FromMilliseconds(50),
    };

    private static StepConfiguration LogStep(string alias, string message) => new()
    {
        Id = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.logMessage",
        Name = alias,
        Alias = alias,
        Settings = new Dictionary<string, object?>
        {
            ["message"] = message,
            ["logLevel"] = "Information",
        },
    };

    /// <summary>
    /// Triggers the automation and waits until the run row reaches <paramref name="status"/>,
    /// then gives the engine a moment more so a step wrongly scheduled after the failure would
    /// have had the chance to record itself.
    /// </summary>
    private async Task<AutomationRun> StartAndWaitAsync(Automation automation, AutomationRunStatus status)
    {
        var runId = await TriggerAsync(automation);
        await WaitForRunStatusAsync(runId, status, TestTimeouts.WorkflowWait);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        return (await _runRepository.GetAsync(runId))!;
    }

    private async Task<Guid> TriggerAsync(Automation automation)
    {
        _automationServiceMock
            .Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var triggerMessage = new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.manual",
            InitiatorType = "system",
        };
        await _handler.HandleAsync(JsonSerializer.Serialize(triggerMessage, JsonOptions.Default), CancellationToken.None);

        return await WaitForRunIdAsync(automation.Id, TestTimeouts.WorkflowWait);
    }

    private async Task<Guid> WaitForRunIdAsync(Guid automationId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var result = await _runRepository.GetPagedByAutomationAsync(automationId);
            if (result.Items.Any())
            {
                return result.Items.First().Id;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"No automation run found within {timeout}.");
    }

    private async Task<AutomationRunStatus> WaitForRunStatusAsync(Guid runId, AutomationRunStatus status, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        AutomationRunStatus? last = null;
        while (DateTime.UtcNow < deadline)
        {
            last = (await _runRepository.GetAsync(runId))?.Status;
            if (last == status)
            {
                return status;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Run {runId} did not reach {status} within {timeout} (last seen: {last}).");
    }

    private async Task WaitForStepRunCountAsync(Guid runId, Guid stepId, StepRunStatus status, int count, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var run = await _runRepository.GetAsync(runId);
            if (run?.StepRuns.Count(s => s.StepId == stepId && s.Status == status) >= count)
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Step {stepId} in run {runId} did not reach {count} x {status} within {timeout}.");
    }

    private async Task<WorkflowInstance> WaitForWorkflowStatusAsync(AutomationRun run, WorkflowStatus status, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var workflowInstanceId = (await _runRepository.GetAsync(run.Id))?.WorkflowInstanceId;
            if (!string.IsNullOrEmpty(workflowInstanceId))
            {
                var instance = await _persistence.GetWorkflowInstance(workflowInstanceId);
                if (instance.Status == status)
                {
                    return instance;
                }
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Workflow for run {run.Id} did not reach {status} within {timeout}.");
    }

    public async Task DisposeAsync()
    {
        await _workflowHost.StopAsync(CancellationToken.None);
        await _provider.DisposeAsync();
        _fixture.Dispose();
    }
}

/// <summary>Test action that always fails with a terminal (Validation) category.</summary>
[Action(StepAlias, "Terminal Failure", Description = "Always fails with a validation error.", Group = "Test")]
public sealed class TerminalFailureAction : ActionBase<FailureStepSettings, FailureStepOutput>
{
    public const string StepAlias = "test.terminalFailure";
    public const string Message = "Settings are invalid";

    public TerminalFailureAction(ActionInfrastructure infrastructure)
        : base(infrastructure)
    {
    }

    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
        => Task.FromResult(ActionResult.Failed(new InvalidOperationException(Message), StepRunErrorCategory.Validation));
}

/// <summary>Test action that always fails with a transient (ServiceUnavailable) category.</summary>
[Action(StepAlias, "Transient Failure", Description = "Always fails with a transient error.", Group = "Test")]
public sealed class TransientFailureAction : ActionBase<FailureStepSettings, FailureStepOutput>
{
    public const string StepAlias = "test.transientFailure";

    public TransientFailureAction(ActionInfrastructure infrastructure)
        : base(infrastructure)
    {
    }

    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
        => Task.FromResult(ActionResult.Failed(new HttpRequestException("Service unavailable"), StepRunErrorCategory.ServiceUnavailable));
}

/// <summary>Settings for the failure test actions (none).</summary>
public sealed class FailureStepSettings
{
}

/// <summary>Output for the failure test actions (none).</summary>
public sealed class FailureStepOutput
{
}
