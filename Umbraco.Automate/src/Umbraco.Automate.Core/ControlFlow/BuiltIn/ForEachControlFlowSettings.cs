using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.ControlFlow.BuiltIn;

/// <summary>
/// Settings for the <see cref="ForEachControlFlow"/>.
/// </summary>
public sealed class ForEachControlFlowSettings
{
    /// <summary>
    /// Gets or sets the binding expression that resolves to the collection to iterate over.
    /// </summary>
    [Field(Label = "Collection",
        Description = "A binding expression that resolves to the collection to iterate (e.g. ${trigger.items}).",
        SupportsBindings = true)]
    public string Collection { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether iterations should run in parallel.
    /// </summary>
    [Field(Label = "Run in parallel",
        Description = "When enabled, all iterations execute simultaneously instead of sequentially.",
        SortOrder = 1)]
    public bool RunParallel { get; set; }
}
