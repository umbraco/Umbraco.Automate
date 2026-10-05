// S1 — Declare fixed outcomes on an action: test harness (docs/plans/action-outcomes/STORIES.md)

using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Testing;

namespace Umbraco.Automate.Tests.Unit.Actions;

public class ActionTestHarnessOutcomeTests
{
    #region Given a harness for the yes/no action

    [Fact]
    public async Task Outcomes_YesNoAction_ReturnsYesThenNo()
    {
        var outcomes = await ActionTestHarness.For<YesNoAction>().GetOutcomesAsync();

        outcomes.Select(o => o.Key).ShouldBe(["yes", "no"]);
    }

    [Fact]
    public async Task EffectiveBranchOutcome_ActionReturnsNoOutcome_IsTheDefault()
    {
        YesNoAction.Returns = null;

        var execution = await ActionTestHarness.For<YesNoAction>().ExecuteWithOutcomeAsync();

        execution.BranchOutcome.ShouldBe("no");
    }

    [Fact]
    public async Task EffectiveBranchOutcome_ActionReturnsYes_IsYes()
    {
        YesNoAction.Returns = "yes";

        var execution = await ActionTestHarness.For<YesNoAction>().ExecuteWithOutcomeAsync();

        execution.BranchOutcome.ShouldBe("yes");
    }

    #endregion

    #region Given a harness for an action with dynamic outcomes

    [Fact]
    public async Task Outcomes_DynamicAction_AreResolvedFromTheSettings()
    {
        var outcomes = await ActionTestHarness.For<DynamicAction>()
            .WithSettings(new DynamicSettings { Cases = "a,b,c" })
            .GetOutcomesAsync();

        outcomes.Select(o => o.Key).ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public async Task EffectiveBranchOutcome_DynamicActionReturnsDeclaredCase_IsThatCase()
    {
        var execution = await ActionTestHarness.For<DynamicAction>()
            .WithSettings(new DynamicSettings { Cases = "a,b,c", Returns = "b" })
            .ExecuteWithOutcomeAsync();

        (execution.BranchOutcome, execution.UndeclaredOutcomeWarning).ShouldBe(("b", null));
    }

    #endregion

    [Fact]
    public void DefaultResolver_NullData_ReturnsNull()
    {
        var resolver = new ActionTestHarness<DynamicAction>.HarnessModelResolver(() => new DynamicSettings());

        resolver.ResolveModel<DynamicSettings>("test.dynamic", null).ShouldBeNull();
    }

    #region Given a harness for an action with outcomes but no default

    [Fact]
    public async Task EffectiveBranchOutcome_NoOutcomeReturnedAndNoDefault_IsNullWithAProblem()
    {
        var execution = await ActionTestHarness.For<NoDefaultAction>().ExecuteWithOutcomeAsync();

        (execution.BranchOutcome, execution.OutcomeProblem is not null).ShouldBe((null, true));
    }

    #endregion

    #region Test Doubles

    // Static state: every test that reads Returns sets it first (xUnit may run other classes in parallel, not this one's tests).
    [Action("test.yesno", "Yes/No Action")]
    private class YesNoAction(ActionInfrastructure infrastructure) : ActionBase<object>(infrastructure)
    {
        public static string? Returns { get; set; }

        public override IReadOnlyList<StepOutcome> GetOutcomes() =>
        [
            new StepOutcome("yes", "Yes"),
            new StepOutcome("no", "No") { IsDefault = true },
        ];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult(Returns is null ? Success() : SuccessWithOutcome(Returns));
    }

    [Action("test.nodefault", "No Default Action")]
    private class NoDefaultAction(ActionInfrastructure infrastructure) : ActionBase<object>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes() =>
        [
            new StepOutcome("yes", "Yes"),
            new StepOutcome("no", "No"),
        ];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult(Success());
    }

    public class DynamicSettings
    {
        public string Cases { get; set; } = string.Empty;

        public string? Returns { get; set; }
    }

    [Action("test.dynamic", "Dynamic Action")]
    private class DynamicAction(ActionInfrastructure infrastructure) : ActionBase<DynamicSettings>(infrastructure)
    {
        public override bool HasDynamicOutcomes => true;

        protected override Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(
            DynamicSettings? settings,
            CancellationToken cancellationToken = default)
        {
            var cases = (settings?.Cases ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);
            return Task.FromResult<IReadOnlyList<StepOutcome>>(cases.Select(c => new StepOutcome(c, c)).ToList());
        }

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult(context.GetSettings<DynamicSettings>().Returns is { } r ? SuccessWithOutcome(r) : Success());
    }

    #endregion
}
