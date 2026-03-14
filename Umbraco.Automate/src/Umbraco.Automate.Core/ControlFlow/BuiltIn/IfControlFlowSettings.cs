using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.ControlFlow.BuiltIn;

/// <summary>
/// Settings for the <see cref="IfControlFlow"/>.
/// </summary>
public sealed class IfControlFlowSettings
{
    /// <summary>
    /// Gets or sets the conditions to evaluate.
    /// </summary>
    [Field(Label = "Conditions",
        Description = "Define conditions to determine which branch to follow.",
        EditorUiAlias = "UmbracoAutomate.PropertyEditorUi.ConditionBuilder")]
    public ConditionSet Conditions { get; set; } = new();
}
