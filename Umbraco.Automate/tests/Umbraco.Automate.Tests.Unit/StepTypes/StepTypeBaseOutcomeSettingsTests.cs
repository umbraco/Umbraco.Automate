using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Tests.Unit.StepTypes;

/// <summary>
/// Which settings a dynamic step type's <c>GetOutcomesAsync</c> sees for the saved settings it is
/// given, through the real <see cref="EditableModelResolver"/>. A step saved as <c>{}</c> runs with
/// its settings type's defaults, so its outcomes must come from those defaults too.
/// </summary>
public class StepTypeBaseOutcomeSettingsTests
{
    private readonly IStepType _action = new OptionsAction(
        new ActionInfrastructure(new EditableModelResolver(new ConfigurationReferenceResolver(new ConfigurationBuilder().Build()))));

    [Fact]
    public async Task EmptySavedSettings_ListsOutcomesFromTheSettingsDefaults()
    {
        var outcomes = await _action.GetOutcomesAsync([]);

        outcomes.Select(o => o.Key).ShouldBe(["yes", "no"]);
    }

    [Fact]
    public async Task EmptySavedSettings_WithAnUnsetRequiredField_StillListsOutcomes()
    {
        // A step just dropped on the canvas: its exits should show, not an "invalid settings" error.
        // The run would fail setup on the required field, so nothing routes on these outcomes.
        var outcomes = await _action.GetOutcomesAsync([]);

        outcomes.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ConfiguredSettings_ListsOutcomesFromTheSavedValues()
    {
        var outcomes = await _action.GetOutcomesAsync(new Dictionary<string, object?> { ["options"] = "a,b", ["endpoint"] = "x" });

        outcomes.Select(o => o.Key).ShouldBe(["a", "b"]);
    }

    [Fact]
    public async Task NoSettingsGiven_ListsOutcomesFromNullSettings()
    {
        var outcomes = await _action.GetOutcomesAsync(null);

        outcomes.ShouldBeEmpty();
    }

    public sealed class OptionsSettings
    {
        [Field(Label = "Options")]
        public string Options { get; set; } = "yes,no";

        [Field(Label = "Endpoint")]
        [Required]
        public string? Endpoint { get; set; }
    }

    [Action("test.options", "Options")]
    private sealed class OptionsAction(ActionInfrastructure infrastructure) : ActionBase<OptionsSettings>(infrastructure)
    {
        public override bool HasDynamicOutcomes => true;

        protected override Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(
            OptionsSettings? settings, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StepOutcome>>(
                (settings?.Options ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(o => new StepOutcome(o, o)).ToList());

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult(ActionResult.Success());
    }
}
