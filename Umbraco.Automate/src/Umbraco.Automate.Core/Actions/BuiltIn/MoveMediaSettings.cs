using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="MoveMediaAction"/>.
/// </summary>
public sealed class MoveMediaSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the media item to move.
    /// </summary>
    [Field(
        Label = "Media",
        Description = "The media item to move.",
        EditorUiAlias = "Umb.PropertyEditorUi.MediaEntityPicker",
        EditorConfig = """[{ "alias": "validationLimit", "value": { "min": 1, "max": 1 } }]""")]
    public string MediaKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key (GUID) of the parent to move the media item under. Leave empty to
    /// move the media item to the root.
    /// </summary>
    [Field(
        Label = "Target Parent",
        Description = "The media folder to move the item under. Leave empty to move to the media root.",
        SortOrder = 1,
        EditorUiAlias = "Umb.PropertyEditorUi.MediaEntityPicker",
        EditorConfig = """[{ "alias": "validationLimit", "value": { "min": 0, "max": 1 } }]""")]
    public string? TargetParentKey { get; set; }
}
