using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// WorkflowCore step body for Parallel container control flow.
/// Branches all children simultaneously and waits for all to complete.
/// </summary>
internal sealed class ParallelContainerStepBody : StepBody
{
    public override ExecutionResult Run(IStepExecutionContext context)
    {
        // Branch all children at once. Each child branch is a separate execution path.
        // WorkflowCore waits for all branches to complete before proceeding.
        return ExecutionResult.Branch(
            [new ControlFlowPersistenceData(nameof(ParallelContainerStepBody))],
            new ControlFlowPersistenceData(nameof(ParallelContainerStepBody)));
    }
}
