namespace Umbraco.Automate.Core.ControlFlow.BuiltIn;

/// <summary>
/// A control flow step that repeatedly executes child steps while conditions are met.
/// Compiled into a WorkflowCore container step with branch routing.
/// </summary>
[ControlFlow("umbracoAutomate.while", "While",
    Description = "Repeatedly executes child steps while conditions are met.",
    Group = "Flow Control",
    Icon = "icon-cycle")]
public sealed class WhileControlFlow : ControlFlowBase<WhileControlFlowSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WhileControlFlow"/> class.
    /// </summary>
    public WhileControlFlow(ControlFlowInfrastructure infrastructure) : base(infrastructure) { }
}
