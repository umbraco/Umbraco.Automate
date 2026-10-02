// S1 — Declare fixed outcomes on an action (docs/plans/action-outcomes/STORIES.md)
//
// Pending: the types under test (StepOutcome, IStepType outcome members) don't exist yet.
// The builder for each named task fills in the body and removes Skip.

namespace Umbraco.Automate.Tests.Unit.StepTypes;

public class StepOutcomeDeclarationTests
{
    #region Given an action declaring yes and no (default)

    [Fact(Skip = "Pending: T1")]
    public void GetOutcomes_YesNoAction_ReturnsKeysInDeclarationOrder()
    {
        // Then the keys are [yes, no], in that order.
    }

    [Fact(Skip = "Pending: T1")]
    public void GetOutcomes_YesNoAction_FlagsOnlyNoAsDefault()
    {
        // Then the single IsDefault outcome is "no".
    }

    #endregion

    #region Given an action that declares nothing

    [Fact(Skip = "Pending: T1")]
    public void GetOutcomes_ActionDeclaringNothing_ReturnsEmpty()
    {
        // Then the outcome list is empty.
    }

    [Fact(Skip = "Pending: T1")]
    public void HasDynamicOutcomes_ActionDeclaringNothing_IsFalse()
    {
        // Then HasDynamicOutcomes is false.
    }

    #endregion
}
