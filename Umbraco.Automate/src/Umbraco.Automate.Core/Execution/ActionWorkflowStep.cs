using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Custom WorkflowCore step that returns a pre-constructed <see cref="ActionStepBody"/>
/// instead of resolving from DI.
/// </summary>
internal sealed class ActionWorkflowStep : WorkflowStep
{
    private readonly ActionStepBody _stepBody;

    public ActionWorkflowStep(ActionStepBody stepBody)
    {
        _stepBody = stepBody;
    }

    public override Type BodyType => typeof(ActionStepBody);

    public override IStepBody ConstructBody(IServiceProvider serviceProvider) => _stepBody;
}
