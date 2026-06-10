using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.ControlFlow.BuiltIn;

/// <summary>
/// Settings for the <see cref="SwitchControlFlow"/>.
/// </summary>
public sealed class SwitchControlFlowSettings
{
    /// <summary>
    /// Gets or sets the cases to evaluate. Cases are evaluated in order; the first match wins.
    /// A "default" outcome is used when no case matches.
    /// </summary>
    [Field(Label = "Cases",
        Description = "Define named cases with conditions. Cases are evaluated in order; the first match wins. A 'default' outcome is used when no case matches.",
        EditorUiAlias = "UmbracoAutomate.PropertyEditorUi.SwitchCaseBuilder",
        SupportsBindings = true)]
    public List<SwitchCase> Cases { get; set; } = [];
}
