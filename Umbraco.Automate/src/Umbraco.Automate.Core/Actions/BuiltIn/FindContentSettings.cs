using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// How the <see cref="FindContentAction"/> matches the <c>Name</c> value against content names.
/// </summary>
public enum FindContentMatchMode
{
    /// <summary>Match the whole name exactly (case-insensitive).</summary>
    Exact = 0,

    /// <summary>
    /// Match names where every word of the value starts a word of the name, in any order,
    /// so <c>"home"</c> matches <c>"Homepage"</c> and <c>"The Homepage"</c>.
    /// </summary>
    StartsWith = 1,

    /// <summary>
    /// Match names containing the value as one run of text. Umbraco Search matches whole words and
    /// word starts, so <c>"bread"</c> matches <c>"Breadcrumbs"</c> and <c>"Bread Basket"</c> but NOT
    /// <c>"Sourbread"</c> — a match in the middle of a word is not found.
    /// </summary>
    Contains = 2,
}

/// <summary>
/// Settings for <see cref="FindContentAction"/>.
/// </summary>
public sealed class FindContentSettings
{
    /// <summary>Gets or sets the name to match against. Required.</summary>
    [Field(
        Label = "Name",
        Description = "The content name to search for.",
        SupportsBindings = true)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content types to restrict the search to (CSV of document-type GUIDs
    /// produced by the <c>DocumentTypePicker</c> property editor). Empty = all types.
    /// </summary>
    [Field(
        Label = "Content types",
        Description = "Restrict the search to these content types. Leave empty to search all types.",
        SortOrder = 1,
        EditorUiAlias = "Umb.PropertyEditorUi.DocumentTypePicker")]
    public string? ContentTypes { get; set; }

    /// <summary>
    /// Gets or sets the match semantics. Stored as a string so the dropdown picker
    /// round-trips cleanly — parsed into <see cref="FindContentMatchMode"/> at execute time.
    /// </summary>
    [Field(
        Label = "Match mode",
        Description = "Exact (default) requires the whole name to match. StartsWith matches names where every word of the text starts a word of the name. Contains needs the text as one run inside the name, but matches whole words and word starts, not text in the middle of a word.",
        SortOrder = 2,
        EditorUiAlias = "Umb.PropertyEditorUi.Dropdown.Single",
        EditorConfig = """[{ "alias": "items", "value": ["Exact", "StartsWith", "Contains"] }]""")]
    public string MatchMode { get; set; } = nameof(FindContentMatchMode.Exact);

    /// <summary>
    /// Gets or sets the culture (e.g. <c>"en-US"</c>) for matching variant content.
    /// Leave empty to search the default language. Invariant content is always included.
    /// </summary>
    [Field(
        Label = "Culture",
        Description = "Culture code (e.g. en-US) for matching variant content names. Leave empty to search the default language. Invariant content is always included.",
        SupportsBindings = true,
        SortOrder = 3)]
    public string? Culture { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include unpublished (draft) content.
    /// <c>false</c> (default) searches the published content index. <c>true</c> searches the draft content index (all content except items in the recycle bin).
    /// </summary>
    [Field(
        Label = "Include unpublished",
        Description = "Include draft and unpublished content in the search.",
        SortOrder = 4,
        EditorUiAlias = "Umb.PropertyEditorUi.Toggle")]
    public bool IncludeUnpublished { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of matches to return. Hard cap to prevent runaway
    /// memory on over-broad queries. Default 50, valid range 1–500.
    /// </summary>
    [Field(
        Label = "Limit",
        Description = "Maximum number of matches to return (1–500).",
        SortOrder = 5,
        EditorUiAlias = "Umb.PropertyEditorUi.Integer")]
    public int Limit { get; set; } = 50;
}
