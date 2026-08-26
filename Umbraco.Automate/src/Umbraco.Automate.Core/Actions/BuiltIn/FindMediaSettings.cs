using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for <see cref="FindMediaAction"/>.
/// </summary>
public sealed class FindMediaSettings
{
    /// <summary>Gets or sets the name to match against. Required.</summary>
    [Field(
        Label = "Name",
        Description = "The media name to search for.",
        SupportsBindings = true)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the media types to restrict the search to (CSV of media-type GUIDs
    /// produced by the <c>MediaTypePicker</c> property editor). Empty = all types.
    /// </summary>
    [Field(
        Label = "Media types",
        Description = "Restrict the search to these media types. Leave empty to search all types.",
        SortOrder = 1,
        EditorUiAlias = "Umb.PropertyEditorUi.MediaTypePicker")]
    public string? MediaTypes { get; set; }

    /// <summary>
    /// Gets or sets the match semantics. Stored as a string so the dropdown picker
    /// round-trips cleanly — parsed into <see cref="FindContentMatchMode"/> at execute time.
    /// </summary>
    [Field(
        Label = "Match mode",
        Description = "Exact (default) requires the whole name to match. StartsWith and Contains are token-based — Contains does not match substrings within a word.",
        SortOrder = 2,
        EditorUiAlias = "Umb.PropertyEditorUi.Dropdown",
        EditorConfig = """[{ "alias": "items", "value": ["Exact", "StartsWith", "Contains"] }]""")]
    public string MatchMode { get; set; } = nameof(FindContentMatchMode.Exact);

    /// <summary>
    /// Gets or sets the maximum number of matches to return. Hard cap to prevent runaway
    /// memory on over-broad queries. Default 50, valid range 1–500.
    /// </summary>
    [Field(
        Label = "Limit",
        Description = "Maximum number of matches to return (1–500).",
        SortOrder = 3,
        EditorUiAlias = "Umb.PropertyEditorUi.Integer")]
    public int Limit { get; set; } = 50;
}
