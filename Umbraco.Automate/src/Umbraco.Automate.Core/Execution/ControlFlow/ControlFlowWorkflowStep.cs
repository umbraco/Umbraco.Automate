using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// Custom WorkflowCore step that returns a pre-constructed control flow step body (or the
/// <see cref="UnavailableStepBody"/> placeholder for a step that can't be resolved)
/// instead of resolving from DI. Analogous to <see cref="ActionWorkflowStep"/> for actions.
/// </summary>
internal sealed class ControlFlowWorkflowStep : WorkflowStep
{
    private readonly StepBody _stepBody;

    public ControlFlowWorkflowStep(StepBody stepBody)
    {
        _stepBody = stepBody;
    }

    public override Type BodyType => _stepBody.GetType();

    public override IStepBody ConstructBody(IServiceProvider serviceProvider) => _stepBody;
}
