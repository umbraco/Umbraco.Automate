using Umbraco.Automate.Core.Settings;

namespace Umbraco.Forms.Automate.Triggers;

/// <summary>
/// Settings shared by form record triggers (submitted, approved).
/// </summary>
public sealed class FormRecordTriggerSettings
{
    /// <summary>
    /// Gets or sets the form IDs to filter on. Leave blank to match all forms.
    /// </summary>
    [Field(
        Label = "Forms",
        Description = "Only fire for these forms. Leave blank to match all.",
        EditorUiAlias = "Forms.PropertyEditorUi.FormPicker.Multiple")]
    public string? FormIds { get; set; }
}
