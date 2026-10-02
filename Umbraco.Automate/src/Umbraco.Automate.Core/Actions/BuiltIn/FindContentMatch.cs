using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A single match returned by <see cref="FindContentAction"/>. Intentionally slim —
/// no property values, no creator/writer keys — so that large result sets stay cheap
/// to serialise through the outbox. Downstream steps needing full content can call
/// <see cref="GetContentAction"/> with the <see cref="ContentKey"/>.
/// </summary>
public sealed class FindContentMatch
{
    /// <summary>Gets the content item's unique key.</summary>
    [Description("The content item's unique key.")]
    public Guid ContentKey { get; init; }

    /// <summary>Gets the content item's name in the matched culture (or invariant name).</summary>
    [Description("The content item's name in the matched culture, or its invariant name.")]
    public string? Name { get; init; }

    /// <summary>Gets the content type alias.</summary>
    [Description("The content type alias.")]
    public string ContentTypeAlias { get; init; } = string.Empty;

    /// <summary>Gets the content type key, if available in the index.</summary>
    [Description("The content type key, if available in the index.")]
    public Guid? ContentTypeKey { get; init; }

    /// <summary>Gets the parent content key, if available in the index.</summary>
    [Description("The parent content key, if available in the index.")]
    public Guid? ParentKey { get; init; }

    /// <summary>Gets the tree level (1 = root). Zero if unavailable from the index.</summary>
    [Description("The tree level (1 = root). Zero if unavailable from the index.")]
    public int Level { get; init; }

    /// <summary>Gets the node path (comma-separated IDs, CMS internal format).</summary>
    [Description("The node path (comma-separated IDs, CMS internal format).")]
    public string? Path { get; init; }

    /// <summary>Gets the published URL for the content item, or null if unpublished.</summary>
    [Description("The published URL for the content item, or null if unpublished.")]
    public string? Url { get; init; }

    /// <summary>Gets the culture the name match was made against, or null for invariant.</summary>
    [Description("The culture the name match was made against, or null for invariant.")]
    public string? Culture { get; init; }

    /// <summary>Gets the creation timestamp (UTC). Default (0001-01-01) if unavailable.</summary>
    [Description("The creation timestamp (UTC). Default (0001-01-01) if unavailable.")]
    public DateTime CreateDate { get; init; }

    /// <summary>Gets the last-edited timestamp (UTC). Default (0001-01-01) if unavailable.</summary>
    [Description("The last-edited timestamp (UTC). Default (0001-01-01) if unavailable.")]
    public DateTime UpdateDate { get; init; }
}
