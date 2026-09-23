using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="CreateContentAction"/>.
/// </summary>
public sealed class CreateContentSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the parent content item the new item is created under, or
    /// empty to create at the content root. Creating at the root additionally requires the
    /// content type to allow it.
    /// </summary>
    [Field(
        Label = "Parent",
        Description = "The content item the new item is created under. Leave empty to create at the content root.",
        EditorUiAlias = "Umb.PropertyEditorUi.DocumentPicker",
        EditorConfig = """[{ "alias": "validationLimit", "value": { "min": 0, "max": 1 } }]""")]
    public string? ParentKey { get; set; }

    /// <summary>
    /// Gets or sets the content type to create, as a content-type GUID produced by the
    /// <c>DocumentTypePicker</c> property editor. Capped at a single selection, and element
    /// types are excluded because content cannot be created from one.
    /// </summary>
    [Field(
        Label = "Content Type",
        Description = "The content type to create.",
        SortOrder = 1,
        EditorUiAlias = "Umb.PropertyEditorUi.DocumentTypePicker",
        EditorConfig = """
            [
                { "alias": "validationLimit", "value": { "min": 1, "max": 1 } },
                { "alias": "onlyPickDocumentTypes", "value": true }
            ]
            """)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the new content item.
    /// </summary>
    [Field(
        Label = "Name",
        Description = "The name of the new content item.",
        SupportsBindings = true,
        SortOrder = 2)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the culture the name applies to. Required when the content type varies by
    /// culture; ignored for invariant content types.
    /// </summary>
    [Field(
        Label = "Culture",
        Description = "Culture code (e.g. en-US). Required if the content type varies by culture.",
        SupportsBindings = true,
        SortOrder = 3)]
    public string? Culture { get; set; }

    /// <summary>
    /// Gets or sets invariant property values to set on creation, as a JSON object
    /// (e.g. {"bodyText": "Hello"}). Property aliases that don't exist on the resolved
    /// content type are silently skipped. Leave empty to create with no property values set.
    /// </summary>
    [Field(
        Label = "Property Values",
        Description = "Invariant property values as JSON (e.g. {\"bodyText\": \"Hello\"}).",
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
