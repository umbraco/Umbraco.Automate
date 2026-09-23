using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="MoveContentAction"/>.
/// </summary>
public sealed class MoveContentSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the content item to move.
    /// </summary>
    [Field(
        Label = "Content",
        Description = "The content item to move.",
        EditorUiAlias = "Umb.PropertyEditorUi.DocumentPicker",
        EditorConfig = """[{ "alias": "validationLimit", "value": { "min": 1, "max": 1 } }]""")]
    public string ContentKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key (GUID) of the parent to move the content under. Leave empty to
    /// move the content to the root.
    /// </summary>
    [Field(
        Label = "Target Parent",
        Description = "The content item to move the item under. Leave empty to move to the content root.",
        SortOrder = 1,
        EditorUiAlias = "Umb.PropertyEditorUi.DocumentPicker",
        EditorConfig = """[{ "alias": "validationLimit", "value": { "min": 0, "max": 1 } }]""")]
    public string? TargetParentKey { get; set; }
}
