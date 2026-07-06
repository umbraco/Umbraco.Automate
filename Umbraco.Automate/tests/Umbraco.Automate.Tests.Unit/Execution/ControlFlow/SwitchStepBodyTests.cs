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

public class SwitchStepBodyTests
{
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly Mock<IAutomationRunRepository> _runRepo = new();

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

        var body = CreateBody(settings);
        var result = body.Run(CreateContext());

        result.OutcomeValue.ShouldBe("caseA");
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

        var body = CreateBody(settings);
        var result = body.Run(CreateContext());

        result.OutcomeValue.ShouldBe("default");
    }

    [Fact]
    public void Run_EmptyCases_ReturnsDefaultOutcome()
    {
        var settings = new SwitchControlFlowSettings { Cases = [] };

        var body = CreateBody(settings);
        var result = body.Run(CreateContext());

        result.OutcomeValue.ShouldBe("default");
    }

    [Fact]
    public void Run_CreatesStepRunWithBranchOutcome()
    {
        StepRun? savedStepRun = null;
        _runRepo.Setup(r => r.SaveStepRunAsync(It.IsAny<StepRun>(), It.IsAny<CancellationToken>()))
            .Callback<StepRun, CancellationToken>((sr, _) => savedStepRun = sr)
            .ReturnsAsync((StepRun sr, CancellationToken _) => sr);

        var settings = new SwitchControlFlowSettings
        {
            Cases =
            [
                new SwitchCase
                {
                    Name = "matchedCase",
                    Conditions = new ConditionSet
                    {
                        Groups = [new ConditionGroup { Conditions = [new Condition { LeftOperand = "a", Operator = ConditionOperator.Equals, RightOperand = "a" }] }],
                    },
                },
            ],
        };

        CreateBody(settings).Run(CreateContext());

        savedStepRun.ShouldNotBeNull();
        savedStepRun.BranchOutcome.ShouldBe("matchedCase");
        savedStepRun.Status.ShouldBe(StepRunStatus.Completed);
    }

    private SwitchStepBody CreateBody(SwitchControlFlowSettings settings)
    {
        StepConfiguration stepConfig = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.switch").WithName("Switch");
        return new SwitchStepBody(stepConfig, settings, _conditionEvaluator, new StepOutputHydrationCache(_runRepo.Object), _runRepo.Object);
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
