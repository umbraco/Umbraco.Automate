using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="CreateContentAction"/>.
/// </summary>
public sealed class CreateContentSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the parent content item the new item is created under.
    /// </summary>
    [Field(
        Label = "Parent Key",
        Description = "The key of the parent content item the new item is created under.",
        SupportsBindings = true)]
    public string ParentKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the alias of the content type to create.
    /// </summary>
    [Field(
        Label = "Content Type Alias",
        Description = "The alias of the content type to create.",
        SupportsBindings = true,
        SortOrder = 1)]
    public string ContentTypeAlias { get; set; } = string.Empty;

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
