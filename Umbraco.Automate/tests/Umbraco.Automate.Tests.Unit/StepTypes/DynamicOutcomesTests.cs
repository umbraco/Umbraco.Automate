// S4 — Outcomes driven by step settings (docs/plans/action-outcomes/STORIES.md)
//
// Pending: the outcome members don't exist yet. Uses a test-only dynamic action whose settings
// hold a list of options plus a default "other". The builder fills in each body and removes Skip.

namespace Umbraco.Automate.Tests.Unit.StepTypes;

public class DynamicOutcomesTests
{
    #region Given typed options a and b

    [Fact(Skip = "Pending: T1")]
    public void HasDynamicOutcomes_DynamicAction_IsTrue()
    {
        // Then HasDynamicOutcomes is true.
    }

    [Fact(Skip = "Pending: T1")]
    public async Task GetOutcomesAsync_OptionsAB_ReturnsAThenBThenOther()
    {
        // When GetOutcomesAsync runs with options [a, b] — Then the keys are [a, b, other].
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T1")]
    public async Task GetOutcomesAsync_OptionsAB_FlagsOtherAsDefault()
    {
        // Then the single default is "other".
        await Task.CompletedTask;
    }

    #endregion

    #region Given the options setting is a binding expression

    [Fact(Skip = "Pending: T1")]
    public async Task GetOutcomesAsync_BoundOptions_ReturnsOnlyOther()
    {
        // Given options "${ steps.x.output.options }" — Then the keys are [other].
        await Task.CompletedTask;
    }

    #endregion

    #region Given empty settings

    [Fact(Skip = "Pending: T1")]
    public async Task GetOutcomesAsync_EmptySettings_ReturnsOnlyOther()
    {
        // Given {} — Then the keys are [other] (empty settings resolve as "no settings").
        await Task.CompletedTask;
    }

    #endregion
}
