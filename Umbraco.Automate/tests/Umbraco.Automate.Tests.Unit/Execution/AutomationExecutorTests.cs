using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Expressions;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Tests.Common.Builders;
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
        var pipeline = new ActionMiddlewarePipeline(new ActionMiddlewareCollection(Array.Empty<IActionMiddleware>));
        var evaluator = new ExpressionEvaluator(new ExpressionFilterCollection(Array.Empty<IExpressionFilter>));

        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<ILogger<ActionStepBody>>());
        var sp = services.BuildServiceProvider();

        _defaultWorkspace = new WorkspaceBuilder().Build();

        _workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_defaultWorkspace);
        _workflowRegistry.Setup(r => r.GetDefinition(It.IsAny<string>(), It.IsAny<int?>()))
            .Returns((WorkflowDefinition?)null);
        _workflowRegistry.Setup(r => r.RegisterWorkflow(It.IsAny<WorkflowDefinition>()))
            .Callback<WorkflowDefinition>(d => _registeredDefinitions.Add(d));
        _workflowHost.Setup(h => h.StartWorkflow(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>()))
            .ReturnsAsync("instance-1");
        _runRepo.Setup(r => r.SaveAsync(It.IsAny<AutomationRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AutomationRun r, CancellationToken _) => r);

        _executor = new AutomationExecutor(
            _workflowHost.Object,
            _workflowRegistry.Object,
            actions,
            pipeline,
            evaluator,
            _runRepo.Object,
            _workspaceService.Object,
            sp,
            Mock.Of<ILogger<AutomationExecutor>>());
    }

    [Fact]
    public async Task ExecuteAsync_CreatesRunRecord()
    {
        var automation = CreateAutomation("testAction");

        await _executor.ExecuteAsync(automation, "user", null, null, CancellationToken.None);

        _runRepo.Verify(r => r.SaveAsync(
            It.Is<AutomationRun>(run => run.AutomationId == automation.Id && run.Status == AutomationRunStatus.Running),
            It.IsAny<CancellationToken>()), Times.Once);
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
