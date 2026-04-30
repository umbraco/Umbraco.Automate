using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Testing.Builders;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution.ControlFlow;

public class ParallelContainerStepBodyTests
{
    private static ParallelContainerStepBody CreateBody(IReadOnlyList<ContainerBranchEdge>? branchEdges = null)
    {
        var evaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        var conditionEvaluator = new ConditionEvaluator(evaluator);
        StepConfiguration stepConfig = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.parallel").WithName("Parallel");
        return new ParallelContainerStepBody(stepConfig, conditionEvaluator, branchEdges ?? Array.Empty<ContainerBranchEdge>());
    }

    [Fact]
    public void Run_WithChildren_BranchesOnePerChild()
    {
        var body = CreateBody();
        var context = CreateContext(childCount: 3);
        var result = body.Run(context);

        result.BranchValues.ShouldNotBeNull();
        result.BranchValues.Count.ShouldBe(3);
        var persistence = result.PersistenceData as ControlPersistenceData;
        persistence.ShouldNotBeNull();
        persistence.ChildrenActive.ShouldBeTrue();
    }

    [Fact]
    public void Run_NoChildren_ReturnsNext()
    {
        var body = CreateBody();
        var context = CreateContext(childCount: 0);
        var result = body.Run(context);

        result.BranchValues.ShouldBeEmpty();
        result.Proceed.ShouldBeTrue();
    }

    [Fact]
    public void Run_BranchValues_ContainIterationContext()
    {
        var body = CreateBody();
        var context = CreateContext(childCount: 2);
        var result = body.Run(context);

        var branch0 = (ForEachIterationContext)result.BranchValues![0];
        var branch1 = (ForEachIterationContext)result.BranchValues[1];

        branch0.Index.ShouldBe(0);
        branch1.Index.ShouldBe(1);
    }

    [Fact]
    public void Run_AllEdgeFiltersFail_SuppressesBranch()
    {
        var alwaysFalse = new ConditionSet
        {
            Groups =
            [
                new ConditionGroup { Conditions = [new Condition { LeftOperand = "a", Operator = ConditionOperator.Equals, RightOperand = "b" }] },
            ],
        };

        var body = CreateBody(branchEdges: [new ContainerBranchEdge(Guid.NewGuid(), alwaysFalse)]);
        var context = CreateContext(childCount: 3);
        var result = body.Run(context);

        result.Proceed.ShouldBeTrue();
        result.BranchValues.ShouldBeEmpty();
    }

    [Fact]
    public void Run_AnyEdgeFilterPasses_BranchesAllChildren()
    {
        var alwaysTrue = new ConditionSet
        {
            Groups =
            [
                new ConditionGroup { Conditions = [new Condition { LeftOperand = "a", Operator = ConditionOperator.Equals, RightOperand = "a" }] },
            ],
        };

        var body = CreateBody(branchEdges: [new ContainerBranchEdge(Guid.NewGuid(), alwaysTrue)]);
        var context = CreateContext(childCount: 3);
        var result = body.Run(context);

        result.BranchValues.ShouldNotBeNull();
        result.BranchValues.Count.ShouldBe(3);
    }

    [Fact]
    public void Run_ReentryWithChildrenComplete_ReturnsNext()
    {
        // After branching, WorkflowCore re-invokes the parent. With ChildrenActive=true and
        // no live descendants in scope (mock has empty ExecutionPointers), IsBranchComplete
        // is vacuously true and the body should converge.
        var body = CreateBody();
        var context = CreateContext(childCount: 2, persistenceData: new ControlPersistenceData { ChildrenActive = true });
        var result = body.Run(context);

        result.Proceed.ShouldBeTrue();
        result.BranchValues.ShouldBeEmpty();
    }

    private static IStepExecutionContext CreateContext(int childCount, object? persistenceData = null)
    {
        var step = new WorkflowStep<ParallelContainerStepBody> { Id = 0 };
        for (var i = 0; i < childCount; i++)
        {
            step.Children.Add(i + 1);
        }

        var context = new Mock<IStepExecutionContext>();
        var workflow = new Mock<WorkflowInstance>();
        workflow.Object.Data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            TriggerOutput = [],
        };
        context.Setup(c => c.Workflow).Returns(workflow.Object);
        context.Setup(c => c.Step).Returns(step);
        context.Setup(c => c.PersistenceData).Returns(persistenceData!);
        context.Setup(c => c.ExecutionPointer).Returns(new ExecutionPointer { Id = Guid.NewGuid().ToString() });
        return context.Object;
    }
}
