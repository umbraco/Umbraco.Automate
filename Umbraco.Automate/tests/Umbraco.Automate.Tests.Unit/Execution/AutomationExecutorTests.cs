using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Testing.Builders;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class AutomationExecutorTests
{
    private readonly Mock<IWorkflowHost> _workflowHost = new();
    private readonly Mock<IWorkflowRegistry> _workflowRegistry = new();
    private readonly Mock<IAutomationRunRepository> _runRepo = new();
    private readonly Mock<IWorkspaceService> _workspaceService = new();
    private readonly AutomationExecutor _executor;

    private readonly Workspace _defaultWorkspace;
    private readonly List<WorkflowDefinition> _registeredDefinitions = [];
    private AutomationWorkflowData? _capturedWorkflowData;

    public AutomationExecutorTests()
    {
        var action = new Mock<IAction>();
        action.Setup(a => a.Alias).Returns("testAction");

        var actions = CreateActionCollection(action.Object);
        var controlFlow = new ControlFlowCollection(Enumerable.Empty<IControlFlow>);
        var pipeline = new ActionMiddlewarePipeline(new ActionMiddlewareCollection(Array.Empty<IActionMiddleware>));
        var evaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        var conditionEvaluator = new ConditionEvaluator(evaluator);

        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<ILogger<ActionStepBody>>());
        services.AddSingleton(Mock.Of<Umbraco.Cms.Core.Events.IEventAggregator>());
        services.Configure<Umbraco.Automate.Core.Configuration.ExecutionOptions>(_ => { });
        var sp = services.BuildServiceProvider();

        var meterFactory = new Mock<IMeterFactory>();
        meterFactory.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts.Name));

        var metrics = new AutomateMetrics(meterFactory.Object);

        var compiler = new WorkflowCompiler(
            actions,
            controlFlow,
            pipeline,
            evaluator,
            new SettingsBindingResolver(evaluator),
            conditionEvaluator,
            _runRepo.Object,
            Mock.Of<IConnectionService>(),
            new DefaultStepErrorClassifier(),
            metrics,
            sp,
            Mock.Of<ILogger<WorkflowCompiler>>());

        _defaultWorkspace = new WorkspaceBuilder().Build();

        _workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_defaultWorkspace);
        _workflowRegistry.Setup(r => r.GetDefinition(It.IsAny<string>(), It.IsAny<int?>()))
            .Returns((WorkflowDefinition?)null);
        _workflowRegistry.Setup(r => r.RegisterWorkflow(It.IsAny<WorkflowDefinition>()))
            .Callback<WorkflowDefinition>(d => _registeredDefinitions.Add(d));
        // AutomationExecutor calls the generic StartWorkflow<AutomationWorkflowData>(id, data).
        // Cover both the 2-arg and 3-arg forms in case overload resolution or default
        // parameters route through either.
        _workflowHost.Setup(h => h.StartWorkflow<AutomationWorkflowData>(
                It.IsAny<string>(),
                It.IsAny<AutomationWorkflowData>()))
            .ReturnsAsync("instance-1");
        _workflowHost.Setup(h => h.StartWorkflow<AutomationWorkflowData>(
                It.IsAny<string>(),
                It.IsAny<AutomationWorkflowData>(),
                It.IsAny<string?>()))
            .ReturnsAsync("instance-1");
        _workflowHost.Setup(h => h.StartWorkflow(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<string>()))
            .ReturnsAsync("instance-1");
        _runRepo.Setup(r => r.SaveAsync(It.IsAny<AutomationRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AutomationRun r, CancellationToken _) => r);

        var circuitBreaker = new Mock<ICircuitBreakerService>();
        circuitBreaker
            .Setup(c => c.IsRunAllowedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _executor = new AutomationExecutor(
            _workflowHost.Object,
            _workflowRegistry.Object,
            compiler,
            _runRepo.Object,
            _workspaceService.Object,
            Mock.Of<IRateLimitService>(),
            circuitBreaker.Object,
            conditionEvaluator,
            metrics,
            Mock.Of<ILogger<AutomationExecutor>>());
    }

    [Fact]
    public async Task ExecuteAsync_CreatesRunRecord()
    {
        var automation = CreateAutomation("testAction");

        await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        _runRepo.Verify(r => r.SaveAsync(
            It.Is<AutomationRun>(run =>
                run.AutomationId == automation.Id &&
                run.Status == AutomationRunStatus.Running &&
                run.WorkspaceId == _defaultWorkspace.Id &&
                run.ServiceAccountKey == _defaultWorkspace.ServiceAccountKey),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PersistsWorkflowInstanceIdViaScopedUpdate()
    {
        var automation = CreateAutomation("testAction");

        await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        // Scoped update so a concurrent RunFinalizer write (e.g. first-step WaitForEvent)
        // cannot be clobbered by re-saving the whole run with stale Status = Running.
        _runRepo.Verify(
            r => r.SetWorkflowInstanceIdAsync(It.IsAny<Guid>(), "instance-1", It.IsAny<CancellationToken>()),
            Times.Once);
        _runRepo.Verify(
            r => r.SaveAsync(It.IsAny<AutomationRun>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RegistersWorkflowDefinition()
    {
        var automation = CreateAutomation("testAction");

        await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        _registeredDefinitions.Count.ShouldBe(1);
        _registeredDefinitions[0].Steps.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsUnknownActions()
    {
        var automation = CreateAutomation("unknownAction");

        await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        _registeredDefinitions.Count.ShouldBe(1);
        _registeredDefinitions[0].Steps.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleSteps_WiresSequentialOutcomes()
    {
        StepConfiguration stepA = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("Step A");
        StepConfiguration stepB = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("Step B");
        StepConfiguration stepC = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("Step C");

        var automation = new AutomationBuilder()
            .AddStep(stepA)
            .AddStep(stepB)
            .AddStep(stepC)
            .Build();

        await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        var def = _registeredDefinitions[0];
        def.Steps.Count.ShouldBe(3);

        // First two steps should have outcome pointing to next
        def.Steps.FindById(0).Outcomes.Count.ShouldBe(1);
        ((ValueOutcome)def.Steps.FindById(0).Outcomes[0]).NextStep.ShouldBe(1);

        def.Steps.FindById(1).Outcomes.Count.ShouldBe(1);
        ((ValueOutcome)def.Steps.FindById(1).Outcomes[0]).NextStep.ShouldBe(2);

        // Last step has no outcomes
        def.Steps.FindById(2).Outcomes.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithConnections_UsesTopologicalOrder()
    {
        // Define steps in reverse order, but connections dictate A → B → C
        StepConfiguration stepA = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("Step A");
        StepConfiguration stepB = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("Step B");
        StepConfiguration stepC = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("Step C");

        var automation = new AutomationBuilder()
            .AddStep(stepC)  // Deliberately reversed
            .AddStep(stepB)
            .AddStep(stepA)
            .WithTriggerConnection(stepA.Id)
            .WithConnection(stepA.Id, stepB.Id)
            .WithConnection(stepB.Id, stepC.Id)
            .Build();

        await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        var def = _registeredDefinitions[0];
        def.Steps.Count.ShouldBe(3);
        def.Steps.FindById(0).Name.ShouldBe("Step A");
        def.Steps.FindById(1).Name.ShouldBe("Step B");
        def.Steps.FindById(2).Name.ShouldBe("Step C");
    }

    [Fact]
    public async Task ExecuteAsync_StartsWorkflowOnHost()
    {
        var automation = CreateAutomation("testAction");

        await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        _workflowHost.Verify(h => h.StartWorkflow(
            It.Is<string>(id => id.StartsWith("automate-")),
            It.IsAny<AutomationWorkflowData>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsRunId()
    {
        var automation = CreateAutomation("testAction");

        var runId = await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        runId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_SetsExecutionContextOnWorkflowData()
    {
        var automation = CreateAutomation("testAction");

        _workflowHost.Setup(h => h.StartWorkflow(It.IsAny<string>(), It.IsAny<AutomationWorkflowData>(), It.IsAny<string>()))
            .ReturnsAsync("instance-1")
            .Callback<string, AutomationWorkflowData, string>((_, data, _) => _capturedWorkflowData = data);

        await _executor.ExecuteAsync(automation, "user", "user@test.com", null, CancellationToken.None);

        _capturedWorkflowData.ShouldNotBeNull();
        _capturedWorkflowData.ExecutionContext.ShouldNotBeNull();
        _capturedWorkflowData.ExecutionContext.ServiceAccountKey.ShouldBe(_defaultWorkspace.ServiceAccountKey);
        _capturedWorkflowData.ExecutionContext.WorkspaceId.ShouldBe(_defaultWorkspace.Id);
        _capturedWorkflowData.ExecutionContext.WorkspaceName.ShouldBe(_defaultWorkspace.Name);
        _capturedWorkflowData.ExecutionContext.AutomationId.ShouldBe(automation.Id);
        _capturedWorkflowData.ExecutionContext.AutomationName.ShouldBe(automation.Name);
        _capturedWorkflowData.ExecutionContext.InitiatorType.ShouldBe("user");
        _capturedWorkflowData.ExecutionContext.InitiatorId.ShouldBe("user@test.com");
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsWhenWorkspaceNotFound()
    {
        _workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);

        var automation = CreateAutomation("testAction");

        await Should.ThrowAsync<InvalidOperationException>(
            () => _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotReRegisterExistingWorkflow()
    {
        var automation = CreateAutomation("testAction");
        var workflowId = $"automate-{automation.Id}-v{automation.Version}";

        _workflowRegistry.Setup(r => r.GetDefinition(workflowId, It.IsAny<int?>()))
            .Returns(new WorkflowDefinition());

        await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        _workflowRegistry.Verify(r => r.RegisterWorkflow(It.IsAny<WorkflowDefinition>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_OutcomeConnections_WiresValueOutcomeWithValue()
    {
        StepConfiguration ifStep = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("If");
        StepConfiguration trueStep = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("True Branch");
        StepConfiguration falseStep = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("False Branch");

        var automation = new AutomationBuilder()
            .AddStep(ifStep)
            .AddStep(trueStep)
            .AddStep(falseStep)
            .WithTriggerConnection(ifStep.Id)
            .WithConnection(ifStep.Id, trueStep.Id, "true")
            .WithConnection(ifStep.Id, falseStep.Id, "false")
            .Build();

        await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        var def = _registeredDefinitions[0];
        def.Steps.Count.ShouldBe(3);

        // If step should have two outcomes with values
        var ifOutcomes = def.Steps.FindById(0).Outcomes;
        ifOutcomes.Count.ShouldBe(2);

        // Both should be ValueOutcome with non-null Value (lambda returning the outcome string)
        var trueOutcome = (ValueOutcome)ifOutcomes[0];
        var falseOutcome = (ValueOutcome)ifOutcomes[1];

        // Verify the outcomes point to correct steps
        trueOutcome.NextStep.ShouldBe(1);
        falseOutcome.NextStep.ShouldBe(2);

        // Verify the outcome values match (GetValue compiles the lambda)
        trueOutcome.GetValue(null!).ShouldBe("true");
        falseOutcome.GetValue(null!).ShouldBe("false");
    }

    [Fact]
    public async Task ExecuteAsync_MixedSequentialAndBranching_WiresCorrectly()
    {
        StepConfiguration stepA = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("Step A");
        StepConfiguration ifStep = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("If");
        StepConfiguration trueStep = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("True Branch");

        var automation = new AutomationBuilder()
            .AddStep(stepA)
            .AddStep(ifStep)
            .AddStep(trueStep)
            .WithTriggerConnection(stepA.Id)
            .WithConnection(stepA.Id, ifStep.Id)         // Sequential (no outcome)
            .WithConnection(ifStep.Id, trueStep.Id, "true")  // Branching
            .Build();

        await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        var def = _registeredDefinitions[0];
        def.Steps.Count.ShouldBe(3);

        // Step A → If (sequential, no outcome value)
        var seqOutcome = (ValueOutcome)def.Steps.FindById(0).Outcomes[0];
        seqOutcome.NextStep.ShouldBe(1);
        seqOutcome.GetValue(null!).ShouldBeNull(); // No Value lambda = sequential

        // If → True Branch (outcome = "true")
        var branchOutcome = (ValueOutcome)def.Steps.FindById(1).Outcomes[0];
        branchOutcome.NextStep.ShouldBe(2);
        branchOutcome.GetValue(null!).ShouldBe("true");
    }

    [Fact]
    public async Task ExecuteAsync_BranchConvergence_BothBranchesPointToSharedStep()
    {
        StepConfiguration ifStep = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("If");
        StepConfiguration trueStep = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("True Branch");
        StepConfiguration falseStep = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("False Branch");
        StepConfiguration sharedEnd = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("Shared End");

        var automation = new AutomationBuilder()
            .AddStep(ifStep)
            .AddStep(trueStep)
            .AddStep(falseStep)
            .AddStep(sharedEnd)
            .WithTriggerConnection(ifStep.Id)
            .WithConnection(ifStep.Id, trueStep.Id, "true")
            .WithConnection(ifStep.Id, falseStep.Id, "false")
            .WithConnection(trueStep.Id, sharedEnd.Id)
            .WithConnection(falseStep.Id, sharedEnd.Id)
            .Build();

        await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        var def = _registeredDefinitions[0];
        def.Steps.Count.ShouldBe(4);

        def.Steps.FindById(0).Name.ShouldBe("If");
        def.Steps.FindById(3).Name.ShouldBe("Shared End");

        var ifOutcomes = def.Steps.FindById(0).Outcomes;
        ifOutcomes.Count.ShouldBe(2);
        ifOutcomes.Cast<ValueOutcome>().Select(o => o.GetValue(null!)).ShouldBe(["true", "false"], ignoreOrder: true);

        var trueStepIndex = def.Steps.FindById(0).Outcomes.Cast<ValueOutcome>()
            .First(o => (string)o.GetValue(null!)! == "true").NextStep;
        var falseStepIndex = def.Steps.FindById(0).Outcomes.Cast<ValueOutcome>()
            .First(o => (string)o.GetValue(null!)! == "false").NextStep;

        var trueStepOutcomes = def.Steps.FindById(trueStepIndex).Outcomes;
        trueStepOutcomes.Count.ShouldBe(1);
        ((ValueOutcome)trueStepOutcomes[0]).NextStep.ShouldBe(3);

        var falseStepOutcomes = def.Steps.FindById(falseStepIndex).Outcomes;
        falseStepOutcomes.Count.ShouldBe(1);
        ((ValueOutcome)falseStepOutcomes[0]).NextStep.ShouldBe(3);

        def.Steps.FindById(3).Outcomes.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_BranchConvergence_TopologicalOrder_ConvergenceStepIsLast()
    {
        StepConfiguration ifStep = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("If");
        StepConfiguration trueStep = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("True Branch");
        StepConfiguration falseStep = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("False Branch");
        StepConfiguration sharedEnd = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("Shared End");

        var automation = new AutomationBuilder()
            .AddStep(sharedEnd)   // Deliberately add convergence step first
            .AddStep(falseStep)
            .AddStep(ifStep)
            .AddStep(trueStep)
            .WithTriggerConnection(ifStep.Id)
            .WithConnection(ifStep.Id, trueStep.Id, "true")
            .WithConnection(ifStep.Id, falseStep.Id, "false")
            .WithConnection(trueStep.Id, sharedEnd.Id)
            .WithConnection(falseStep.Id, sharedEnd.Id)
            .Build();

        await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        var def = _registeredDefinitions[0];
        def.Steps.FindById(0).Name.ShouldBe("If");
        def.Steps.FindById(3).Name.ShouldBe("Shared End");
    }

    [Fact]
    public async Task ExecuteAsync_BranchConvergence_WithTailSteps_WiresDiamondThenSequential()
    {
        StepConfiguration ifStep = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("If");
        StepConfiguration trueStep = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("True Branch");
        StepConfiguration falseStep = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("False Branch");
        StepConfiguration sharedMerge = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("Shared Merge");
        StepConfiguration finalStep = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("Final Step");

        var automation = new AutomationBuilder()
            .AddStep(ifStep)
            .AddStep(trueStep)
            .AddStep(falseStep)
            .AddStep(sharedMerge)
            .AddStep(finalStep)
            .WithTriggerConnection(ifStep.Id)
            .WithConnection(ifStep.Id, trueStep.Id, "true")
            .WithConnection(ifStep.Id, falseStep.Id, "false")
            .WithConnection(trueStep.Id, sharedMerge.Id)
            .WithConnection(falseStep.Id, sharedMerge.Id)
            .WithConnection(sharedMerge.Id, finalStep.Id)
            .Build();

        await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        var def = _registeredDefinitions[0];
        def.Steps.Count.ShouldBe(5);

        var mergeIndex = Enumerable.Range(0, 5)
            .First(i => def.Steps.FindById(i).Name == "Shared Merge");
        var finalIndex = Enumerable.Range(0, 5)
            .First(i => def.Steps.FindById(i).Name == "Final Step");

        var mergeOutcomes = def.Steps.FindById(mergeIndex).Outcomes;
        mergeOutcomes.Count.ShouldBe(1);
        ((ValueOutcome)mergeOutcomes[0]).NextStep.ShouldBe(finalIndex);

        def.Steps.FindById(finalIndex).Outcomes.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_NoConnections_FallsBackToSequential()
    {
        StepConfiguration stepA = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("Step A");
        StepConfiguration stepB = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("Step B");

        var automation = new AutomationBuilder()
            .AddStep(stepA)
            .AddStep(stepB)
            .Build();

        await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        var def = _registeredDefinitions[0];
        def.Steps.FindById(0).Outcomes.Count.ShouldBe(1);
        ((ValueOutcome)def.Steps.FindById(0).Outcomes[0]).NextStep.ShouldBe(1);
        ((ValueOutcome)def.Steps.FindById(0).Outcomes[0]).GetValue(null!).ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_TriggerEdgeFilterPasses_StartsWorkflowAsNormal()
    {
        StepConfiguration step = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("Step");
        var filter = MakeEqualsFilter("${ trigger.country }", "DK");

        var automation = new AutomationBuilder()
            .AddStep(step)
            .WithConnection(Guid.Empty, step.Id, outcome: null, filter: filter)
            .Build();

        var triggerData = new Dictionary<string, object?> { ["country"] = "DK" };
        await _executor.ExecuteAsync(automation, "user", null, triggerData, CancellationToken.None);

        _workflowHost.Verify(h => h.StartWorkflow(
            It.IsAny<string>(),
            It.IsAny<AutomationWorkflowData>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_TriggerEdgeFilterFails_SkipsWorkflowAndCompletesRun()
    {
        StepConfiguration step = new StepConfigurationBuilder().WithActionAlias("testAction").WithName("Step");
        var filter = MakeEqualsFilter("${ trigger.country }", "DK");

        var automation = new AutomationBuilder()
            .AddStep(step)
            .WithConnection(Guid.Empty, step.Id, outcome: null, filter: filter)
            .Build();

        AutomationRun? savedRun = null;
        _runRepo.Setup(r => r.SaveAsync(It.IsAny<AutomationRun>(), It.IsAny<CancellationToken>()))
            .Callback<AutomationRun, CancellationToken>((r, _) => savedRun = r)
            .ReturnsAsync((AutomationRun r, CancellationToken _) => r);

        var triggerData = new Dictionary<string, object?> { ["country"] = "SE" };
        await _executor.ExecuteAsync(automation, "user", null, triggerData, CancellationToken.None);

        // Workflow must NOT have been started.
        _workflowHost.Verify(h => h.StartWorkflow(
            It.IsAny<string>(),
            It.IsAny<AutomationWorkflowData>(),
            It.IsAny<string>()), Times.Never);

        // Run record must have been finalised as Completed (no work to do).
        savedRun.ShouldNotBeNull();
        savedRun.Status.ShouldBe(AutomationRunStatus.Completed);
        savedRun.CompletedUtc.ShouldNotBeNull();
    }

    private static ConditionSet MakeEqualsFilter(string left, string right)
        => new()
        {
            Groups =
            [
                new ConditionGroup
                {
                    Conditions =
                    [
                        new Condition { LeftOperand = left, Operator = ConditionOperator.Equals, RightOperand = right },
                    ],
                },
            ],
        };

    private static Automation CreateAutomation(string actionAlias) =>
        new AutomationBuilder()
            .AddStep(new StepConfigurationBuilder().WithActionAlias(actionAlias).WithName("Step 1"))
            .Build();

    private static ActionCollection CreateActionCollection(params IAction[] actions)
    {
        IEnumerable<IAction> items = actions;
        return new ActionCollection(() => items);
    }
}
