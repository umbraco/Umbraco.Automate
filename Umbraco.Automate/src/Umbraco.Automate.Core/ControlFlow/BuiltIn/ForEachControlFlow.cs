namespace Umbraco.Automate.Core.ControlFlow.BuiltIn;

/// <summary>
/// A control flow step that iterates over a collection, executing child steps for each item.
/// Compiled into a WorkflowCore container step with branch routing.
/// </summary>
[ControlFlow("umbracoAutomate.forEach", "For Each",
    Description = "Iterates over a collection, executing child steps for each item.",
    Group = "Flow Control",
    Icon = "icon-repeat")]
public sealed class ForEachControlFlow : ControlFlowBase<ForEachControlFlowSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForEachControlFlow"/> class.
    /// </summary>
    public ForEachControlFlow(ControlFlowInfrastructure infrastructure) : base(infrastructure) { }
}
