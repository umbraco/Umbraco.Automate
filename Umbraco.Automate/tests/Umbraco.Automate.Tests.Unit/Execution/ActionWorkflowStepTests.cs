using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Execution;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution;

/// <summary>
/// <see cref="ActionWorkflowStep.PrimeForRetry"/> runs before WorkflowCore retries or resumes a failed
/// step. A payload that was not an approval decision is dropped so the step asks again; a real
/// decision is kept so a retry still applies it.
/// </summary>
public class ActionWorkflowStepTests
{
    [Fact]
    public void PrimeForRetry_EventWithoutADecision_ForgetsTheEvent()
    {
        var pointer = new ExecutionPointer { EventPublished = true, EventData = "not a decision" };

        PrimeForRetry(pointer);

        pointer.EventPublished.ShouldBeFalse();
    }

    [Fact]
    public void PrimeForRetry_EventWithoutADecision_DropsThePayload()
    {
        var pointer = new ExecutionPointer { EventPublished = true, EventData = "not a decision" };

        PrimeForRetry(pointer);

        pointer.EventData.ShouldBeNull();
    }

    [Fact]
    public void PrimeForRetry_EventWithADecision_KeepsTheEvent()
    {
        var pointer = new ExecutionPointer
        {
            EventPublished = true,
            EventData = new ApprovalDecision { Outcome = ApprovalOutcome.Approved },
        };

        PrimeForRetry(pointer);

        pointer.EventPublished.ShouldBeTrue();
    }

    private static void PrimeForRetry(ExecutionPointer pointer)
    {
        // The step body is never invoked by PrimeForRetry, so none of its collaborators matter.
        var step = new ActionWorkflowStep(null!);
        step.PrimeForRetry(pointer);
    }
}
