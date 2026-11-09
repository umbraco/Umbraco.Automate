using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="CreateElementAction"/>.
/// </summary>
public sealed class CreateElementSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the Library folder the new element is created in, or empty
    /// to create at the Library root.
    /// </summary>
    [Field(
        Label = "Folder",
        Description = "The Library folder the new element is created in. Leave empty to create at the Library root.",
        EditorUiAlias = Constants.EditorUiAliases.ElementKeyPicker,
        EditorConfig = """[{ "alias": "folderOnly", "value": true }]""",
        SupportsBindings = true,
        BindingMustResolve = true)]
    public string? FolderKey { get; set; }

    /// <summary>
    /// Gets or sets the element type to create, as a content-type GUID produced by the
    /// <c>ua-document-type-picker</c> editor UI (<c>Constants.EditorUiAliases.DocumentTypePicker</c>). Capped at a single selection and limited to
    /// element types.
    /// </summary>
    [Field(
        Label = "Element Type",
        Description = "The element type to create. Only types with \"Allow in Library\" turned on can be created.",
        SortOrder = 1,
        EditorUiAlias = Constants.EditorUiAliases.DocumentTypePicker,
        EditorConfig = """
            [
                { "alias": "validationLimit", "value": { "min": 1, "max": 1 } },
                { "alias": "onlyPickElementTypes", "value": true }
            ]
            """)]
    [Required]
    public string ElementType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the new Library element.
    /// </summary>
    [Field(
        Label = "Name",
        Description = "The name of the new Library element.",
        SupportsBindings = true,
        SortOrder = 2)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the culture the name applies to. Required when the element type varies by
    /// culture; ignored for invariant element types.
    /// </summary>
    [Field(
        Label = "Culture",
        Description = "Culture code (e.g. en-US). Required if the element type varies by culture.",
        SupportsBindings = true,
        SortOrder = 3)]
    public string? Culture { get; set; }

    /// <summary>
    /// Gets or sets property values to set on creation, as a JSON object
    /// (e.g. {"bodyText": "Hello"}). Culture-variant properties are set for <see cref="Culture"/>.
    /// Property aliases that don't exist on the element type are skipped with a warning.
    /// </summary>
    [Field(
        Label = "Property Values",
        Description = "Property values as JSON (e.g. {\"bodyText\": \"Hello\"}). Culture-variant properties are set for the chosen culture.",
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
