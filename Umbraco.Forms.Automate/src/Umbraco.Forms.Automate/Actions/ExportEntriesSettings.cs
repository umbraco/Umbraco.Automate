using Umbraco.Automate.Core.Settings;

namespace Umbraco.Forms.Automate.Actions;

/// <summary>
/// Settings for the <see cref="ExportEntriesAction"/>.
/// </summary>
public sealed class ExportEntriesSettings
{
    /// <summary>
    /// Gets or sets the form ID to export entries from.
    /// </summary>
    [Field(Label = "Form", Description = "The form to export entries from.", EditorUiAlias = "Forms.PropertyEditorUi.FormPicker.Single", SupportsBindings = true)]
    public string FormId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of entries to export.
    /// </summary>
    [Field(Label = "Page Size", Description = "Maximum number of entries to export (default: 100).", SortOrder = 1)]
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to only export approved entries.
    /// </summary>
    [Field(
        Label = "Approved Only",
        Description = "When checked, only export approved entries.",
        SortOrder = 2,
        EditorUiAlias = "Umb.PropertyEditorUi.Toggle")]
    public bool ApprovedOnly { get; set; }
}
