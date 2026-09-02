using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="StartAutomationAction"/>.
/// </summary>
public sealed class StartAutomationSettings
{
    /// <summary>
    /// Gets or sets the key of the automation to start. The automation must be published
    /// and belong to the same workspace as the automation running this step.
    /// </summary>
    [Field(
        Label = "Automation",
        Description = "The automation to start. It must be published and belong to the same workspace.",
        EditorUiAlias = "Umb.Automate.AutomationPicker")]
    public string AutomationKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional JSON object handed to the started automation as its trigger
    /// output — its steps read the values via <c>${ trigger.yourKey }</c> bindings.
    /// </summary>
    [Field(
        Label = "Trigger Data",
        Description = "Optional JSON object passed to the started automation as its trigger output. Its steps can read the values with ${ trigger.yourKey } bindings.",
        SupportsBindings = true,
        EditorUiAlias = "Umb.PropertyEditorUi.CodeEditor",
        EditorConfig = """[{ "alias": "language", "value": "json" }, { "alias": "wordWrap", "value": true }]""",
        SortOrder = 1)]
    public string? TriggerData { get; set; }
}
