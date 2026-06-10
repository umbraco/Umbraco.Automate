namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that sets a named variable for use in downstream steps.
/// </summary>
[Action("umbracoAutomate.setVariable", "Set Variable",
    Description = "Sets a named variable for use in downstream steps.",
    Group = "Utilities",
    Icon = "icon-brackets")]
public sealed class SetVariableAction : ActionBase<SetVariableSettings, SetVariableOutput>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetVariableAction"/> class.
    /// </summary>
    public SetVariableAction(ActionInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<SetVariableSettings>();

        return Task.FromResult(Success(new SetVariableOutput
        {
            Name = settings.Name,
            Value = settings.Value,
        }));
    }
}
