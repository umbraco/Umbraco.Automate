namespace Umbraco.Automate.Core.ControlFlow.BuiltIn;

/// <summary>
/// A control flow step that evaluates conditions and routes to the true or false branch.
/// Compiled into a lightweight outcome-routed step body by the workflow compiler.
/// </summary>
[ControlFlow("umbracoAutomate.if", "If",
    Description = "Evaluates conditions and routes to the true or false branch.",
    Group = "Control Flow",
    Icon = "icon-split")]
public sealed class IfControlFlow : ControlFlowBase<IfControlFlowSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IfControlFlow"/> class.
    /// </summary>
    public IfControlFlow(ControlFlowInfrastructure infrastructure) : base(infrastructure) { }
}
