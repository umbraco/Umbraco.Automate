using Umbraco.Automate.Core.Conditions;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that evaluates conditions and routes to the true or false branch.
/// </summary>
[Action("umbracoAutomate.if", "If",
    Description = "Evaluates conditions and routes to the true or false branch.",
    Group = "Flow Control",
    Icon = "icon-split")]
public sealed class IfAction : ActionBase<IfActionSettings>
{
    private readonly ConditionEvaluator _conditionEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="IfAction"/> class.
    /// </summary>
    public IfAction(ActionInfrastructure infrastructure, ConditionEvaluator conditionEvaluator)
        : base(infrastructure)
    {
        _conditionEvaluator = conditionEvaluator;
    }

    /// <inheritdoc />
    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<IfActionSettings>();
        var bindingData = context.BindingData ?? new Dictionary<string, object?>();

        var result = _conditionEvaluator.Evaluate(settings.Conditions, bindingData);
        var outcome = result ? "true" : "false";

        return Task.FromResult(ActionResult.SuccessWithOutcome(outcome, new { Result = result }));
    }
}
