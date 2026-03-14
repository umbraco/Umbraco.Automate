using Shouldly;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Bindings;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution.ControlFlow;

public class SwitchStepBodyTests
{
    private readonly ConditionEvaluator _conditionEvaluator;

    public SwitchStepBodyTests()
    {
        var evaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        _conditionEvaluator = new ConditionEvaluator(evaluator);
    }

    [Fact]
    public void Run_FirstCaseMatches_ReturnsFirstCaseOutcome()
    {
        var settings = new SwitchControlFlowSettings
        {
            Cases =
            [
                new SwitchCase
                {
                    Name = "caseA",
                    Conditions = new ConditionSet
                    {
                        Groups = [new ConditionGroup { Conditions = [new Condition { LeftOperand = "x", Operator = ConditionOperator.Equals, RightOperand = "x" }] }],
                    },
                },
                new SwitchCase
                {
                    Name = "caseB",
                    Conditions = new ConditionSet
                    {
                        Groups = [new ConditionGroup { Conditions = [new Condition { LeftOperand = "y", Operator = ConditionOperator.Equals, RightOperand = "y" }] }],
                    },
                },
            ],
        };

        var body = new SwitchStepBody(settings, _conditionEvaluator);
        var result = body.Run(CreateContext());

        result.OutcomeValue.ShouldBe("caseA");
    }

    [Fact]
    public void Run_SecondCaseMatches_ReturnsSecondCaseOutcome()
    {
        var settings = new SwitchControlFlowSettings
        {
            Cases =
            [
                new SwitchCase
                {
                    Name = "caseA",
                    Conditions = new ConditionSet
                    {
                        Groups = [new ConditionGroup { Conditions = [new Condition { LeftOperand = "x", Operator = ConditionOperator.Equals, RightOperand = "y" }] }],
                    },
                },
                new SwitchCase
                {
                    Name = "caseB",
                    Conditions = new ConditionSet
                    {
                        Groups = [new ConditionGroup { Conditions = [new Condition { LeftOperand = "y", Operator = ConditionOperator.Equals, RightOperand = "y" }] }],
                    },
                },
            ],
        };

        var body = new SwitchStepBody(settings, _conditionEvaluator);
        var result = body.Run(CreateContext());

        result.OutcomeValue.ShouldBe("caseB");
    }

    [Fact]
    public void Run_NoCaseMatches_ReturnsDefaultOutcome()
    {
        var settings = new SwitchControlFlowSettings
        {
            Cases =
            [
                new SwitchCase
                {
                    Name = "caseA",
                    Conditions = new ConditionSet
                    {
                        Groups = [new ConditionGroup { Conditions = [new Condition { LeftOperand = "x", Operator = ConditionOperator.Equals, RightOperand = "y" }] }],
                    },
                },
            ],
        };

        var body = new SwitchStepBody(settings, _conditionEvaluator);
        var result = body.Run(CreateContext());

        result.OutcomeValue.ShouldBe("default");
    }

    [Fact]
    public void Run_EmptyCases_ReturnsDefaultOutcome()
    {
        var settings = new SwitchControlFlowSettings { Cases = [] };

        var body = new SwitchStepBody(settings, _conditionEvaluator);
        var result = body.Run(CreateContext());

        result.OutcomeValue.ShouldBe("default");
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
