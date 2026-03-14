using Shouldly;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Bindings;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution.ControlFlow;

public class IfStepBodyTests
{
    private readonly ConditionEvaluator _conditionEvaluator;

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

        var body = new IfStepBody(settings, _conditionEvaluator);
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

        var body = new IfStepBody(settings, _conditionEvaluator);
        var result = body.Run(CreateContext());

        result.Proceed.ShouldBeTrue();
        result.OutcomeValue.ShouldBe("false");
    }

    [Fact]
    public void Run_EmptyConditionSet_ReturnsTrueOutcome()
    {
        var settings = new IfControlFlowSettings { Conditions = new ConditionSet() };

        var body = new IfStepBody(settings, _conditionEvaluator);
        var result = body.Run(CreateContext());

        result.OutcomeValue.ShouldBe("true");
    }

    private static IStepExecutionContext CreateContext()
    {
        var context = new Mock<IStepExecutionContext>();
        var workflow = new Mock<WorkflowCore.Models.WorkflowInstance>();
        workflow.Object.Data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            TriggerOutput = [],
        };
        context.Setup(c => c.Workflow).Returns(workflow.Object);
        return context.Object;
    }
}
