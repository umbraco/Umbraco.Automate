using Umbraco.Automate.Core.Conditions;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// WorkflowCore step body for Parallel container control flow.
/// Branches all children simultaneously. WorkflowCore waits for all branches
/// to complete before proceeding to the convergence step.
/// </summary>
internal sealed class ParallelContainerStepBody : StepBody
{
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly IReadOnlyList<ContainerBranchEdge> _branchEdges;

    public ParallelContainerStepBody(
        ConditionEvaluator conditionEvaluator,
        IReadOnlyList<ContainerBranchEdge> branchEdges)
    {
        _conditionEvaluator = conditionEvaluator;
        _branchEdges = branchEdges;
    }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        // Create one branch per child step. WorkflowCore's executor uses the branch list
        // combined with step.Children to spawn parallel execution pointers.
        // Each item in the branch list becomes context.Item for the child execution.
        var childCount = context.Step.Children.Count;

        if (childCount == 0)
        {
            return ExecutionResult.Next();
        }

        // Outgoing-edge filters gate the parallel branch as a whole. If every outgoing
        // edge has a filter and they all fail, suppress the branch entirely. (We cannot
        // skip individual children selectively because WorkflowCore cross-products
        // BranchValues × step.Children when spawning child pointers.)
        var data = (AutomationWorkflowData)context.Workflow.Data;
        var bindingData = BindingDataBuilder.Build(data);

        if (!ContainerBranchEdge.AnyEdgePasses(_branchEdges, _conditionEvaluator, bindingData))
        {
            return ExecutionResult.Next();
        }

        var branches = Enumerable.Range(0, childCount)
            .Select(i => (object)new ForEachIterationContext(null, i))
            .ToList();

        return ExecutionResult.Branch(branches, new ControlFlowPersistenceData(nameof(ParallelContainerStepBody)));
    }
}
