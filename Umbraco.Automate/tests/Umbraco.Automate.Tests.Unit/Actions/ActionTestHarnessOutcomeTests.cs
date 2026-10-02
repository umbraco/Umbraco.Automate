// S1 — Declare fixed outcomes on an action: test harness (docs/plans/action-outcomes/STORIES.md)
//
// Pending: ActionTestHarness has no outcome support yet. The builder fills in each body and removes Skip.

namespace Umbraco.Automate.Tests.Unit.Actions;

public class ActionTestHarnessOutcomeTests
{
    #region Given a harness for the yes/no action

    [Fact(Skip = "Pending: T10")]
    public async Task Outcomes_YesNoAction_ReturnsYesThenNo()
    {
        // When the test asks for the action's outcomes — Then the keys are [yes, no].
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T10")]
    public async Task EffectiveBranchOutcome_ActionReturnsNoOutcome_IsTheDefault()
    {
        // When the action returns success without an outcome — Then the effective branch outcome is "no".
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T10")]
    public async Task EffectiveBranchOutcome_ActionReturnsYes_IsYes()
    {
        // When the action returns outcome "yes" — Then the effective branch outcome is "yes".
        await Task.CompletedTask;
    }

    #endregion
}
