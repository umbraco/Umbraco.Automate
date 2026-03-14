using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.ControlFlow.BuiltIn;

/// <summary>
/// Settings for the <see cref="WhileControlFlow"/>.
/// </summary>
public sealed class WhileControlFlowSettings
{
    /// <summary>
    /// Gets or sets the conditions to evaluate before each iteration.
    /// </summary>
    [Field(Label = "Conditions",
        Description = "Define conditions that must be true for the loop to continue.",
        EditorUiAlias = "UmbracoAutomate.PropertyEditorUi.ConditionBuilder")]
    public ConditionSet Conditions { get; set; } = new();

    /// <summary>
    /// Gets or sets the maximum number of iterations allowed (safety guard).
    /// </summary>
    [Field(Label = "Max iterations",
        Description = "Maximum number of iterations to prevent infinite loops. Default is 100.",
        SortOrder = 1)]
    public int MaxIterations { get; set; } = 100;
}
