using Shouldly;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution.ControlFlow;

public class ParallelContainerStepBodyTests
{
    private static ParallelContainerStepBody CreateBody(IReadOnlyList<ContainerBranchEdge>? branchEdges = null)
    {
        var evaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        var conditionEvaluator = new ConditionEvaluator(evaluator);
        return new ParallelContainerStepBody(conditionEvaluator, branchEdges ?? Array.Empty<ContainerBranchEdge>());
    }

    [Fact]
    public void Run_WithChildren_BranchesOnePerChild()
    {
        var body = CreateBody();
        var context = CreateContext(childCount: 3);
        var result = body.Run(context);

        result.BranchValues.ShouldNotBeNull();
        result.BranchValues.Count.ShouldBe(3);
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

    private static IStepExecutionContext CreateContext(int childCount)
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
        return context.Object;
    }
}
