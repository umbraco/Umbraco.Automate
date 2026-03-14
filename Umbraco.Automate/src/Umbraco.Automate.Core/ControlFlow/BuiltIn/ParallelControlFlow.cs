namespace Umbraco.Automate.Core.ControlFlow.BuiltIn;

/// <summary>
/// A control flow step that executes all child branches simultaneously.
/// Compiled into a WorkflowCore container step with branch routing.
/// </summary>
[ControlFlow("umbracoAutomate.parallel", "Parallel",
    Description = "Executes all child branches simultaneously.",
    Group = "Flow Control",
    Icon = "icon-split-alt")]
public sealed class ParallelControlFlow : ControlFlowBase<object>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelControlFlow"/> class.
    /// </summary>
    public ParallelControlFlow(ControlFlowInfrastructure infrastructure) : base(infrastructure) { }
}
