namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that evaluates an expression and routes to a matching case branch.
/// </summary>
[Action("umbracoAutomate.switch", "Switch",
    Description = "Evaluates an expression and routes to a matching case branch.",
    Group = "Flow Control",
    Icon = "icon-merge")]
public sealed class SwitchAction : ActionBase<SwitchActionSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SwitchAction"/> class.
    /// </summary>
    public SwitchAction(ActionInfrastructure infrastructure)
        : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<SwitchActionSettings>();

        var cases = settings.Cases
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var matchedCase = cases.FirstOrDefault(c =>
            string.Equals(c, settings.Expression, StringComparison.OrdinalIgnoreCase));

        var outcome = matchedCase ?? "default";

        return Task.FromResult(ActionResult.SuccessWithOutcome(
            outcome,
            new { Expression = settings.Expression, MatchedCase = matchedCase }));
    }
}
