using Shouldly;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Testing.Builders;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution.ControlFlow;

public class IfStepBodyTests
{
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly Mock<IAutomationRunRepository> _runRepo = new();

    public IfStepBodyTests()
    {
        var evaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        _conditionEvaluator = new ConditionEvaluator(evaluator);
    }

    [Fact]
    public void Run_ConditionsTrue_ReturnsTrueOutcome()
    {
        var settings = new IfControlFlowSettings
        {
            Conditions = new ConditionSet
            {
                Groups =
                [
                    new ConditionGroup
                    {
                        Conditions = [new Condition { LeftOperand = "yes", Operator = ConditionOperator.Equals, RightOperand = "yes" }],
                    },
                ],
            },
        };

        var body = CreateBody(settings);
        var result = body.Run(CreateContext());

        result.Proceed.ShouldBeTrue();
        result.OutcomeValue.ShouldBe("true");
    }

    [Fact]
    public void Run_ConditionsFalse_ReturnsFalseOutcome()
    {
        var settings = new IfControlFlowSettings
        {
            Conditions = new ConditionSet
            {
                Groups =
                [
                    new ConditionGroup
                    {
                        Conditions = [new Condition { LeftOperand = "yes", Operator = ConditionOperator.Equals, RightOperand = "no" }],
                    },
                ],
            },
        };

        var body = CreateBody(settings);
        var result = body.Run(CreateContext());

        result.Proceed.ShouldBeTrue();
        result.OutcomeValue.ShouldBe("false");
    }

    [Fact]
    public void Run_EmptyConditionSet_ReturnsTrueOutcome()
    {
        var settings = new IfControlFlowSettings { Conditions = new ConditionSet() };

        var body = CreateBody(settings);
        var result = body.Run(CreateContext());

        result.OutcomeValue.ShouldBe("true");
    }

    [Fact]
    public void Run_CreatesStepRunWithBranchOutcome()
    {
        StepRun? savedStepRun = null;
        _runRepo.Setup(r => r.SaveStepRunAsync(It.IsAny<StepRun>(), It.IsAny<CancellationToken>()))
            .Callback<StepRun, CancellationToken>((sr, _) => savedStepRun = sr)
            .ReturnsAsync((StepRun sr, CancellationToken _) => sr);

        var settings = new IfControlFlowSettings
        {
            Conditions = new ConditionSet
            {
                Groups = [new ConditionGroup { Conditions = [new Condition { LeftOperand = "a", Operator = ConditionOperator.Equals, RightOperand = "a" }] }],
            },
        };

        CreateBody(settings).Run(CreateContext());

        savedStepRun.ShouldNotBeNull();
        savedStepRun.BranchOutcome.ShouldBe("true");
        savedStepRun.Status.ShouldBe(StepRunStatus.Completed);
        savedStepRun.ActionAlias.ShouldBe("umbracoAutomate.if");
    }

    private IfStepBody CreateBody(IfControlFlowSettings settings)
    {
        StepConfiguration stepConfig = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.if").WithName("If");
        return new IfStepBody(stepConfig, settings, _conditionEvaluator, new StepOutputHydrationCache(_runRepo.Object), _runRepo.Object);
    }

    private static IStepExecutionContext CreateContext()
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
        return context.Object;
    }
}
