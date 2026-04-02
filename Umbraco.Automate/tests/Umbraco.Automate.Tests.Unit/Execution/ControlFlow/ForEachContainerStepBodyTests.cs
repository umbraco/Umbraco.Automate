using Shouldly;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Testing.Builders;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution.ControlFlow;

public class ForEachContainerStepBodyTests
{
    private readonly BindingEvaluator _bindingEvaluator;
    private readonly Mock<IAutomationRunRepository> _runRepo = new();

    public ForEachContainerStepBodyTests()
    {
        _bindingEvaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        _runRepo.Setup(r => r.SaveStepRunAsync(It.IsAny<StepRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StepRun sr, CancellationToken _) => sr);
    }

    [Fact]
    public void Run_EmptyCollection_ReturnsNext()
    {
        var body = CreateBody(new ForEachControlFlowSettings { Collection = "" });
        var result = body.Run(CreateContext());

        result.Proceed.ShouldBeTrue();
        result.BranchValues.ShouldBeEmpty();
    }

    [Fact]
    public void Run_CommaSeparatedCollection_Parallel_BranchesAllItems()
    {
        var body = CreateBody(new ForEachControlFlowSettings { Collection = "a, b, c", RunParallel = true });
        var result = body.Run(CreateContext());

        result.BranchValues.ShouldNotBeNull();
        result.BranchValues.Count.ShouldBe(3);
    }

    [Fact]
    public void Run_JsonArrayCollection_Parallel_BranchesAllItems()
    {
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            TriggerOutput = new Dictionary<string, object?> { ["items"] = "[\"x\",\"y\"]" },
        };
        var body = CreateBody(new ForEachControlFlowSettings
        {
            Collection = "${trigger.items}",
            RunParallel = true,
        });
        var result = body.Run(CreateContext(data));

        result.BranchValues.ShouldNotBeNull();
        result.BranchValues.Count.ShouldBe(2);
    }

    [Fact]
    public void Run_Sequential_FirstIteration_BranchesSingleItem()
    {
        var body = CreateBody(new ForEachControlFlowSettings { Collection = "a, b, c", RunParallel = false });
        var result = body.Run(CreateContext());

        result.BranchValues.ShouldNotBeNull();
        result.BranchValues.Count.ShouldBe(1);

        var iteration = (ForEachIterationContext)result.BranchValues[0];
        iteration.Index.ShouldBe(0);
        iteration.Item.ShouldBe("a");
    }

    [Fact]
    public void Run_Sequential_ResumeIteration_AdvancesIndex()
    {
        var body = CreateBody(new ForEachControlFlowSettings { Collection = "a, b, c", RunParallel = false });
        var persistence = new ControlFlowPersistenceData(nameof(ForEachContainerStepBody)) { Metadata = "1" };
        var result = body.Run(CreateContext(persistenceData: persistence));

        result.BranchValues.ShouldNotBeNull();
        var iteration = (ForEachIterationContext)result.BranchValues[0];
        iteration.Index.ShouldBe(2);
        iteration.Item.ShouldBe("c");
    }

    [Fact]
    public void Run_Sequential_LastIteration_ReturnsNext()
    {
        var body = CreateBody(new ForEachControlFlowSettings { Collection = "a, b", RunParallel = false });
        var persistence = new ControlFlowPersistenceData(nameof(ForEachContainerStepBody)) { Metadata = "1" };
        var result = body.Run(CreateContext(persistenceData: persistence));

        result.BranchValues.ShouldBeEmpty();
        result.Proceed.ShouldBeTrue();
    }

    [Fact]
    public void Run_TracksStepRunWithIterationTotal()
    {
        StepRun? saved = null;
        _runRepo.Setup(r => r.SaveStepRunAsync(It.IsAny<StepRun>(), It.IsAny<CancellationToken>()))
            .Callback<StepRun, CancellationToken>((sr, _) => saved = sr)
            .ReturnsAsync((StepRun sr, CancellationToken _) => sr);

        var body = CreateBody(new ForEachControlFlowSettings { Collection = "a, b, c", RunParallel = true });
        body.Run(CreateContext());

        saved.ShouldNotBeNull();
        saved.IterationTotal.ShouldBe(3);
    }

    private ForEachContainerStepBody CreateBody(ForEachControlFlowSettings settings)
    {
        StepConfiguration stepConfig = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.forEach").WithName("ForEach");
        return new ForEachContainerStepBody(stepConfig, settings, _bindingEvaluator, _runRepo.Object);
    }

    private static IStepExecutionContext CreateContext(
        AutomationWorkflowData? data = null,
        object? persistenceData = null)
    {
        data ??= new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            TriggerOutput = [],
        };
        var context = new Mock<IStepExecutionContext>();
        var workflow = new Mock<WorkflowInstance>();
        workflow.Object.Data = data;
        context.Setup(c => c.Workflow).Returns(workflow.Object);
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        context.Setup(c => c.PersistenceData).Returns(persistenceData);
        return context.Object;
    }
}
