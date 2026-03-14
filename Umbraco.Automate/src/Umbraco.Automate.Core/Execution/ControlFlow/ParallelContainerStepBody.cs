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

        var branches = Enumerable.Range(0, childCount)
            .Select(i => (object)new ForEachIterationContext(null, i))
            .ToList();

        return ExecutionResult.Branch(branches, new ControlFlowPersistenceData(nameof(ParallelContainerStepBody)));
    }
}
