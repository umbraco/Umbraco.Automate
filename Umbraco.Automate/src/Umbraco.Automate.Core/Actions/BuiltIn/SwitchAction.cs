using Umbraco.Automate.Core.Conditions;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that evaluates cases with conditions and routes to the first matching case branch.
/// </summary>
[Obsolete("Use SwitchControlFlow instead. This action will be removed in a future version.")]
[Action("umbracoAutomate.switch", "Switch",
    Description = "Evaluates cases with conditions and routes to the first matching branch.",
    Group = "Flow Control",
    Icon = "icon-merge")]
public sealed class SwitchAction : ActionBase<SwitchActionSettings>
{
    private readonly ConditionEvaluator _conditionEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwitchAction"/> class.
    /// </summary>
    public SwitchAction(ActionInfrastructure infrastructure, ConditionEvaluator conditionEvaluator)
        : base(infrastructure)
    {
        _conditionEvaluator = conditionEvaluator;
    }

    /// <inheritdoc />
    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<SwitchActionSettings>();
        var bindingData = context.BindingData ?? new Dictionary<string, object?>();

        foreach (var switchCase in settings.Cases)
        {
            if (_conditionEvaluator.Evaluate(switchCase.Conditions, bindingData))
            {
                return Task.FromResult(ActionResult.SuccessWithOutcome(
                    switchCase.Name,
                    new { MatchedCase = switchCase.Name }));
            }
        }

        return Task.FromResult(ActionResult.SuccessWithOutcome(
            "default",
            new { MatchedCase = (string?)null }));
    }
}
