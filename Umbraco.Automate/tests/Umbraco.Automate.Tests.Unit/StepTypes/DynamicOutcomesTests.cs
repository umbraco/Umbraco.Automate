// S4 — Outcomes driven by step settings (docs/plans/action-outcomes/STORIES.md)
//
// Uses a test-only dynamic action whose settings hold a list of options plus a default "other".

using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Tests.Unit.StepTypes;

public class DynamicOutcomesTests
{
    private static readonly ActionInfrastructure ActionDeps = CreateActionInfrastructure();

    #region Given typed options a and b

    [Fact]
    public void HasDynamicOutcomes_DynamicAction_IsTrue()
    {
        IStepType action = new OptionsAction(ActionDeps);

        action.HasDynamicOutcomes.ShouldBeTrue();
    }

    [Fact]
    public async Task GetOutcomesAsync_OptionsAB_ReturnsAThenBThenOther()
    {
        IStepType action = new OptionsAction(ActionDeps);

        var outcomes = await action.GetOutcomesAsync(new Dictionary<string, object?> { ["options"] = new List<string> { "a", "b" } });

        outcomes.Select(o => o.Key).ShouldBe(["a", "b", "other"]);
    }

    [Fact]
    public async Task GetOutcomesAsync_OptionsAB_FlagsOtherAsDefault()
    {
        IStepType action = new OptionsAction(ActionDeps);

        var outcomes = await action.GetOutcomesAsync(new Dictionary<string, object?> { ["options"] = new List<string> { "a", "b" } });

        outcomes.Where(o => o.IsDefault).Select(o => o.Key).ShouldBe(["other"]);
    }

    #endregion

    #region Given the options setting is a binding expression

    [Fact(Skip = "Pending: T2")]
    public async Task GetOutcomesAsync_BoundOptions_ReturnsOnlyOther()
    {
        // Given options "${ steps.x.output.options }" — Then the keys are [other].
        await Task.CompletedTask;
    }

    #endregion

    #region Given empty settings

    [Fact]
    public async Task GetOutcomesAsync_EmptySettings_ReturnsOnlyOther()
    {
        IStepType action = new OptionsAction(ActionDeps);

        var outcomes = await action.GetOutcomesAsync([]);

        outcomes.Select(o => o.Key).ShouldBe(["other"]);
    }

    #endregion

    private static ActionInfrastructure CreateActionInfrastructure()
    {
        var resolver = new Mock<IEditableModelResolver>();
        resolver.Setup(r => r.ResolveModel<OptionsSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<EditableModelSchema?>()))
            .Returns((string _, object? data, EditableModelSchema? _) =>
            {
                if (data is not Dictionary<string, object?> dict)
                {
                    return null;
                }

                return new OptionsSettings
                {
                    Options = dict.TryGetValue("options", out var v) && v is List<string> list ? list : [],
                };
            });
        return new ActionInfrastructure(resolver.Object);
    }

    #region Test Doubles

    private class OptionsSettings
    {
        public List<string> Options { get; set; } = [];
    }

    [Action("test.options", "Options Action")]
    private class OptionsAction(ActionInfrastructure infrastructure) : ActionBase<OptionsSettings>(infrastructure)
    {
        public override bool HasDynamicOutcomes => true;

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        protected override Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(
            OptionsSettings? settings, CancellationToken cancellationToken)
        {
            var outcomes = (settings?.Options ?? [])
                .Select(o => new StepOutcome(o, o))
                .Append(new StepOutcome("other", "Other") { IsDefault = true })
                .ToList();

            return Task.FromResult<IReadOnlyList<StepOutcome>>(outcomes);
        }
    }

    #endregion
}
