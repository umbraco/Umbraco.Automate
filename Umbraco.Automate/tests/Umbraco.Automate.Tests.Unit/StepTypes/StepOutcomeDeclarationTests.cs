// S1 — Declare fixed outcomes on an action (docs/plans/action-outcomes/STORIES.md)

using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Tests.Unit.StepTypes;

public class StepOutcomeDeclarationTests
{
    private static readonly ActionInfrastructure ActionDeps = new(new Mock<IEditableModelResolver>().Object);

    #region Given an action declaring yes and no (default)

    [Fact]
    public void GetOutcomes_YesNoAction_ReturnsKeysInDeclarationOrder()
    {
        IStepType action = new YesNoAction(ActionDeps);

        action.GetOutcomes().Select(o => o.Key).ShouldBe(["yes", "no"]);
    }

    [Fact]
    public void GetOutcomes_YesNoAction_FlagsOnlyNoAsDefault()
    {
        IStepType action = new YesNoAction(ActionDeps);

        action.GetOutcomes().Where(o => o.IsDefault).Select(o => o.Key).ShouldBe(["no"]);
    }

    #endregion

    #region Given an action that declares nothing

    [Fact]
    public void GetOutcomes_ActionDeclaringNothing_ReturnsEmpty()
    {
        IStepType action = new PlainAction(ActionDeps);

        action.GetOutcomes().ShouldBeEmpty();
    }

    [Fact]
    public void HasDynamicOutcomes_ActionDeclaringNothing_IsFalse()
    {
        IStepType action = new PlainAction(ActionDeps);

        action.HasDynamicOutcomes.ShouldBeFalse();
    }

    #endregion

    #region Test Doubles

    [Action("test.yesno", "Yes/No Action")]
    private class YesNoAction(ActionInfrastructure infrastructure) : ActionBase<object>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes() =>
        [
            new StepOutcome("yes", "Yes"),
            new StepOutcome("no", "No") { IsDefault = true },
        ];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.plain", "Plain Action")]
    private class PlainAction(ActionInfrastructure infrastructure) : ActionBase<object>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    #endregion
}
