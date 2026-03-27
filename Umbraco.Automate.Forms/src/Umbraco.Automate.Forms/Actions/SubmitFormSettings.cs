using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Forms.Actions;

/// <summary>
/// Settings for the <see cref="SubmitFormAction"/>.
/// </summary>
public sealed class SubmitFormSettings
{
    /// <summary>
    /// Gets or sets the form ID to submit to.
    /// </summary>
    [Field(Label = "Form ID", Description = "The GUID of the form to submit to.", SupportsBindings = true)]
    public string FormId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the field values as a JSON object mapping field GUIDs to values.
    /// </summary>
    [Field(
        Label = "Field Values (JSON)",
        Description = "JSON object mapping field GUIDs to values, e.g. {\"guid\": \"value\"}. Supports ${ binding } syntax.",
        SortOrder = 1,
        EditorUiAlias = "Umb.PropertyEditorUi.TextArea",
        SupportsBindings = true)]
    public string FieldValuesJson { get; set; } = string.Empty;
}
