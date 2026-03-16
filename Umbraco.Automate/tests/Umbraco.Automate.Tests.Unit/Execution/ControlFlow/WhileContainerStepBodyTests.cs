using Shouldly;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Tests.Common.Builders;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution.ControlFlow;

public class WhileContainerStepBodyTests
{
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly Mock<IAutomationRunRepository> _runRepo = new();

    public WhileContainerStepBodyTests()
    {
        var evaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        _conditionEvaluator = new ConditionEvaluator(evaluator);
        _runRepo.Setup(r => r.SaveStepRunAsync(It.IsAny<StepRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StepRun sr, CancellationToken _) => sr);
    }

    [Fact]
    public void Run_ConditionsTrue_BranchesChildren()
    {
        var body = CreateBody(AlwaysTrueSettings());
        var result = body.Run(CreateContext());

        result.BranchValues.ShouldNotBeNull();
        result.BranchValues.Count.ShouldBe(1);
    }

    [Fact]
    public void Run_ConditionsFalse_ReturnsNext()
    {
        var body = CreateBody(AlwaysFalseSettings());
        var result = body.Run(CreateContext());

        result.BranchValues.ShouldBeEmpty();
        result.Proceed.ShouldBeTrue();
    }

    [Fact]
    public void Run_MaxIterationsReached_ReturnsNext()
    {
        var settings = AlwaysTrueSettings();
        settings.MaxIterations = 5;

        var body = CreateBody(settings);
        var persistence = new ControlFlowPersistenceData(nameof(WhileContainerStepBody)) { Metadata = "5" };
        var result = body.Run(CreateContext(persistenceData: persistence));

        result.BranchValues.ShouldBeEmpty();
        result.Proceed.ShouldBeTrue();
    }

    [Fact]
    public void Run_TracksIterationCount_InPersistenceData()
    {
        var body = CreateBody(AlwaysTrueSettings());
        var result = body.Run(CreateContext());

        var persistence = result.PersistenceData as ControlFlowPersistenceData;
        persistence.ShouldNotBeNull();
        persistence.Metadata.ShouldBe("1");
    }

    [Fact]
    public void Run_CorruptedPersistenceData_TreatsAsFirstIteration()
    {
        var body = CreateBody(AlwaysTrueSettings());
        var persistence = new ControlFlowPersistenceData(nameof(WhileContainerStepBody)) { Metadata = "not-a-number" };
        var result = body.Run(CreateContext(persistenceData: persistence));

        // Should not throw, treats as iteration 0.
        result.BranchValues.ShouldNotBeNull();
    }

    [Fact]
    public void Run_TracksStepRunOnExit()
    {
        StepRun? saved = null;
        _runRepo.Setup(r => r.SaveStepRunAsync(It.IsAny<StepRun>(), It.IsAny<CancellationToken>()))
            .Callback<StepRun, CancellationToken>((sr, _) => saved = sr)
            .ReturnsAsync((StepRun sr, CancellationToken _) => sr);

        var body = CreateBody(AlwaysFalseSettings());
        body.Run(CreateContext());

        saved.ShouldNotBeNull();
        saved.IterationTotal.ShouldBe(0);
        saved.Status.ShouldBe(StepRunStatus.Completed);
    }

    private WhileContainerStepBody CreateBody(WhileControlFlowSettings settings)
    {
        StepConfiguration stepConfig = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.while").WithName("While");
        return new WhileContainerStepBody(stepConfig, settings, _conditionEvaluator, _runRepo.Object);
    }

    private static WhileControlFlowSettings AlwaysTrueSettings() => new()
    {
        Conditions = new ConditionSet
        {
            Groups = [new ConditionGroup { Conditions = [new Condition { LeftOperand = "a", Operator = ConditionOperator.Equals, RightOperand = "a" }] }],
        },
    };

    private static WhileControlFlowSettings AlwaysFalseSettings() => new()
    {
        Conditions = new ConditionSet
        {
            Groups = [new ConditionGroup { Conditions = [new Condition { LeftOperand = "a", Operator = ConditionOperator.Equals, RightOperand = "b" }] }],
        },
    };

    private static IStepExecutionContext CreateContext(object? persistenceData = null)
    {
        var context = new Mock<IStepExecutionContext>();
        var workflow = new Mock<WorkflowInstance>();
        workflow.Object.Data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            TriggerOutput = [],
        };
        context.Setup(c => c.Workflow).Returns(workflow.Object);
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        context.Setup(c => c.PersistenceData).Returns(persistenceData);
        return context.Object;
    }
}
