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
    private readonly ForEachCollectionCache _collectionCache;
    private readonly StepOutputHydrationCache _hydrationCache;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly IReadOnlyList<ContainerBranchEdge> _branchEdges;

    public ParallelContainerStepBody(
        StepConfiguration stepConfig,
        ForEachCollectionCache collectionCache,
        StepOutputHydrationCache hydrationCache,
        ConditionEvaluator conditionEvaluator,
        IReadOnlyList<ContainerBranchEdge> branchEdges)
    {
        _stepConfig = stepConfig;
        _collectionCache = collectionCache;
        _hydrationCache = hydrationCache;
        _conditionEvaluator = conditionEvaluator;
        _branchEdges = branchEdges;
    }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        var data = (AutomationWorkflowData)context.Workflow.Data;
        var parentIteration = context.Item as ForEachIterationContext;

        // Re-entry: wait for the parallel branches to drain before converging.
        if (context.PersistenceData is ControlPersistenceData persistence && persistence.ChildrenActive)
        {
            if (!context.Workflow.IsBranchComplete(context.ExecutionPointer.Id))
            {
                return ExecutionResult.Persist(persistence);
            }

            // All branches drained together — their scoped outputs can never be
            // read again, so prune them to stop the persisted blob growing.
            IterationScopePruner.PruneContainerScopes(data, _stepConfig.Id, parentIteration);
            return ExecutionResult.Next();
        }

        // Fan-out comes from step.Children, not from the branch list. WorkflowCore's
        // ExecutionResultProcessor spawns one child pointer per (branch value × child step)
        // pair, so a branch value per child would run every branch once per branch — three
        // parallel branches would execute nine times. WorkflowCore's own Sequence primitive
        // branches with a single value for exactly this reason, and we mirror it.
        var childCount = context.Step.Children.Count;

        if (childCount == 0)
        {
            return ExecutionResult.Next();
        }

        // Outgoing-edge filters gate the parallel branch as a whole. If every outgoing
        // edge has a filter and they all fail, suppress the branch entirely. (We cannot
        // skip individual children selectively because WorkflowCore cross-products
        // BranchValues × step.Children when spawning child pointers.)
        var bindingData = BindingDataBuilder.Build(data, parentIteration, _collectionCache, _hydrationCache, context.CancellationToken);

        if (!ContainerBranchEdge.AnyEdgePasses(_branchEdges, _conditionEvaluator, bindingData))
        {
            return ExecutionResult.Next();
        }

        // One context, shared by every branch. WorkflowCore carries it to each child pointer
        // as ContextItem, giving the whole fan-out a single iteration scope that
        // PruneContainerScopes clears on convergence. Branches therefore share the scoped
        // `previous` pointer — a fan-out has no per-branch predecessor to track, so steps
        // inside a branch should reference each other by alias rather than via `previous`.
        var branch = new ForEachIterationContext(null, 0, _stepConfig.Id, parentIteration);

        return ExecutionResult.Branch([branch], new ControlPersistenceData { ChildrenActive = true });
    }
}
