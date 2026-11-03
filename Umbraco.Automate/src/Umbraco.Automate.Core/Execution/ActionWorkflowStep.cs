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

    /// <summary>
    /// Called by WorkflowCore's Retry and Suspend error handlers before the failed step runs again.
    /// A step woken by an event whose payload was not an approval decision failed because of that
    /// payload, and the pointer still carries it: left as-is, the next attempt (e.g. after an
    /// operator resumes a Suspended run) would re-read the same payload and never reach a decision.
    /// Forgetting the event makes the step start over and wait for a real decision again. A valid
    /// decision is kept, so a retry after a transient failure still applies the decision that was
    /// submitted.
    /// </summary>
    public override void PrimeForRetry(ExecutionPointer pointer)
    {
        base.PrimeForRetry(pointer);

        if (pointer.EventPublished && ApprovalDecisionReader.Read(pointer.EventData) is null)
        {
            pointer.EventPublished = false;
            pointer.EventData = null;
        }
    }
}
