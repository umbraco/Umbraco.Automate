using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="CreateMediaAction"/>.
/// </summary>
public sealed class CreateMediaSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the parent folder the new item is created in, or empty to
    /// create at the media root. The CMS media entity picker offers folders only, since a media
    /// item cannot be created inside another media item.
    /// </summary>
    [Field(
        Label = "Parent",
        Description = "The media folder the new item is created in. Leave empty to create at the media root.",
        EditorUiAlias = "Umb.PropertyEditorUi.MediaEntityPicker",
        EditorConfig = """[{ "alias": "validationLimit", "value": { "min": 0, "max": 1 } }]""")]
    public string? ParentKey { get; set; }

    /// <summary>
    /// Gets or sets the media type to create, as a media-type GUID produced by the
    /// <c>MediaTypePicker</c> property editor. Capped at a single selection.
    /// </summary>
    [Field(
        Label = "Media Type",
        Description = "The media type to create.",
        SortOrder = 1,
        EditorUiAlias = "Umb.PropertyEditorUi.MediaTypePicker",
        EditorConfig = """[{ "alias": "validationLimit", "value": { "min": 1, "max": 1 } }]""")]
    public string MediaType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the new media item.
    /// </summary>
    [Field(
        Label = "Name",
        Description = "The name of the new media item.",
        SupportsBindings = true,
        SortOrder = 2)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL of the file to download and store on the item's upload property.
    /// A URL is the only file source an automation has, so this is how the file gets set —
    /// typically bound from an earlier step, e.g. <c>${ loop.item.image.medium }</c>.
    /// Leave empty for media types that hold no file, such as folders.
    /// </summary>
    [Field(
        Label = "Source URL",
        Description = "URL of the file to download and store on the media item. Leave empty for media types that hold no file, such as folders.",
        SupportsBindings = true,
        SortOrder = 3)]
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Gets or sets invariant property values to set on creation, as a JSON object
    /// (e.g. {"alt": "Sunset"}). Property aliases that don't exist on the resolved media type
    /// are silently skipped. Leave empty to create with no property values set.
    /// <para>
    /// This writes raw values and cannot upload a file — use <see cref="SourceUrl"/> for the
    /// item's file rather than naming the upload property here.
    /// </para>
    /// </summary>
    [Field(
        Label = "Property Values",
        Description = "Invariant property values as JSON (e.g. {\"alt\": \"Sunset over the harbour\"}). Cannot set the file — use Source URL for that.",
        SortOrder = 4,
        SupportsBindings = true,
        EditorUiAlias = "Umb.PropertyEditorUi.CodeEditor",
        EditorConfig = """
            [
                { "alias": "language", "value": "json" },
                { "alias": "height", "value": 150 },
                { "alias": "wordWrap", "value": true }
            ]
            """)]
    public string? PropertiesJson { get; set; }
}
