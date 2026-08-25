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
    private static ParallelContainerStepBody CreateBody(
        IReadOnlyList<ContainerBranchEdge>? branchEdges = null,
        StepConfiguration? stepConfig = null)
    {
        var evaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        var conditionEvaluator = new ConditionEvaluator(evaluator);
        var hydrationCache = new StepOutputHydrationCache(Mock.Of<Umbraco.Automate.Core.Runs.IAutomationRunRepository>());
        stepConfig ??= new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.parallel").WithName("Parallel");
        return new ParallelContainerStepBody(stepConfig, new ForEachCollectionCache(evaluator, hydrationCache), hydrationCache, conditionEvaluator, branchEdges ?? Array.Empty<ContainerBranchEdge>());
    }

    // WorkflowCore spawns one child pointer per (branch value × child step) pair, so the branch
    // count must stay at one however many children there are — otherwise each parallel branch
    // runs once per branch. See ParallelContainerStepBody.Run.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void Run_WithChildren_BranchesExactlyOnce(int childCount)
    {
        var body = CreateBody();
        var context = CreateContext(childCount);
        var result = body.Run(context);

        result.BranchValues.ShouldNotBeNull();
        result.BranchValues.Count.ShouldBe(1);
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
    public void Run_BranchValue_IsSingleContainerScopedIterationContext()
    {
        StepConfiguration stepConfig = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.parallel").WithName("Parallel");

        var body = CreateBody(stepConfig: stepConfig);
        var context = CreateContext(childCount: 2);
        var result = body.Run(context);

        var branch = (ForEachIterationContext)result.BranchValues!.Single();

        branch.Index.ShouldBe(0);
        branch.ContainerStepId.ShouldBe(stepConfig.Id);
        branch.ScopePath.ShouldBe($"{stepConfig.Id:N}:0");
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
    public void Run_AnyEdgeFilterPasses_Branches()
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
        result.BranchValues.Count.ShouldBe(1);
        result.Proceed.ShouldBeFalse();
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

    [Fact]
    public void Run_ReentryWithChildrenComplete_PrunesIterationScopes()
    {
        StepConfiguration stepConfig = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.parallel").WithName("Parallel");
        var scope0 = $"{stepConfig.Id:N}:0";
        var scope1 = $"{stepConfig.Id:N}:1";

        var data = CreateData();
        data.IterationStepOutputs[scope0] = new() { [Guid.NewGuid()] = new() { ["message"] = "branch-0" } };
        data.IterationStepOutputs[scope1] = new() { [Guid.NewGuid()] = new() { ["message"] = "branch-1" } };
        data.IterationLastCompletedStepId[scope0] = Guid.NewGuid();

        var body = CreateBody(stepConfig: stepConfig);
        var context = CreateContext(childCount: 2, persistenceData: new ControlPersistenceData { ChildrenActive = true }, data: data);
        var result = body.Run(context);

        result.Proceed.ShouldBeTrue();
        data.IterationStepOutputs.ShouldBeEmpty();
        data.IterationLastCompletedStepId.ShouldBeEmpty();
    }

    private static AutomationWorkflowData CreateData() => new()
    {
        RunId = Guid.NewGuid(),
        AutomationId = Guid.NewGuid(),
        TriggerOutput = [],
    };

    private static IStepExecutionContext CreateContext(int childCount, object? persistenceData = null, AutomationWorkflowData? data = null)
    {
        var step = new WorkflowStep<ParallelContainerStepBody> { Id = 0 };
        for (var i = 0; i < childCount; i++)
        {
            step.Children.Add(i + 1);
        }

        var context = new Mock<IStepExecutionContext>();
        var workflow = new Mock<WorkflowInstance>();
        workflow.Object.Data = data ?? CreateData();
        context.Setup(c => c.Workflow).Returns(workflow.Object);
        context.Setup(c => c.Step).Returns(step);
        context.Setup(c => c.PersistenceData).Returns(persistenceData!);
        context.Setup(c => c.ExecutionPointer).Returns(new ExecutionPointer { Id = Guid.NewGuid().ToString() });
        return context.Object;
    }
}
