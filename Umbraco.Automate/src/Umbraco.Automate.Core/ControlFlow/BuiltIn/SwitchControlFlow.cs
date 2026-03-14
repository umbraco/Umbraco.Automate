namespace Umbraco.Automate.Core.ControlFlow.BuiltIn;

/// <summary>
/// A control flow step that evaluates cases with conditions and routes to the first matching branch.
/// Compiled into a lightweight outcome-routed step body by the workflow compiler.
/// </summary>
[ControlFlow("umbracoAutomate.switch", "Switch",
    Description = "Evaluates cases with conditions and routes to the first matching branch.",
    Group = "Flow Control",
    Icon = "icon-merge")]
public sealed class SwitchControlFlow : ControlFlowBase<SwitchControlFlowSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SwitchControlFlow"/> class.
    /// </summary>
    public SwitchControlFlow(ControlFlowInfrastructure infrastructure) : base(infrastructure) { }
}
