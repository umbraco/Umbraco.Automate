using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Primitives;

namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// WorkflowCore step body for Parallel container control flow.
/// Branches all children simultaneously, then waits for them to drain via
/// <see cref="WorkflowInstance.IsBranchComplete"/> before converging.
/// </summary>
internal sealed class ParallelContainerStepBody : ContainerStepBody
{
    private readonly StepConfiguration _stepConfig;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly IReadOnlyList<ContainerBranchEdge> _branchEdges;

    public ParallelContainerStepBody(
        StepConfiguration stepConfig,
        ConditionEvaluator conditionEvaluator,
        IReadOnlyList<ContainerBranchEdge> branchEdges)
    {
        _stepConfig = stepConfig;
        _conditionEvaluator = conditionEvaluator;
        _branchEdges = branchEdges;
    }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        // Re-entry: wait for the parallel branches to drain before converging.
        if (context.PersistenceData is ControlPersistenceData persistence && persistence.ChildrenActive)
        {
            if (!context.Workflow.IsBranchComplete(context.ExecutionPointer.Id))
            {
                return ExecutionResult.Persist(persistence);
            }
            return ExecutionResult.Next();
        }

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
        var parentIteration = context.Item as ForEachIterationContext;
        var bindingData = BindingDataBuilder.Build(data, parentIteration);

        if (!ContainerBranchEdge.AnyEdgePasses(_branchEdges, _conditionEvaluator, bindingData))
        {
            return ExecutionResult.Next();
        }

        var branches = Enumerable.Range(0, childCount)
            .Select(i => (object)new ForEachIterationContext(null, i, _stepConfig.Id, parentIteration))
            .ToList();

        return ExecutionResult.Branch(branches, new ControlPersistenceData { ChildrenActive = true });
    }
}
