using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="GetContentAction"/>.
/// </summary>
public sealed class GetContentOutput
{
    /// <summary>Gets the content item's unique key.</summary>
    [Description("The content item's unique key.")]
    public Guid ContentKey { get; init; }

    /// <summary>Gets the content item's name in the requested culture, or its invariant name.</summary>
    [Description("The content item's name in the requested culture, or its invariant name.")]
    public string? Name { get; init; }

    /// <summary>Gets the content type alias.</summary>
    [Description("The content type alias.")]
    public string? ContentTypeAlias { get; init; }

    /// <summary>Gets the content type key.</summary>
    [Description("The content type key.")]
    public Guid ContentTypeKey { get; init; }

    /// <summary>Gets the parent content key, if any.</summary>
    [Description("The parent content key, if any.")]
    public Guid? ParentKey { get; init; }

    /// <summary>Gets the tree level (1 = root).</summary>
    [Description("The tree level (1 = root).")]
    public int Level { get; init; }

    /// <summary>Gets the node path (comma-separated IDs, CMS internal format).</summary>
    [Description("The node path (comma-separated IDs, CMS internal format).")]
    public string? Path { get; init; }

    /// <summary>Gets the sort order among siblings.</summary>
    [Description("The sort order among siblings.")]
    public int SortOrder { get; init; }

    /// <summary>Gets the URL for the content item in the requested culture.</summary>
    [Description("The URL for the content item in the requested culture.")]
    public string? Url { get; init; }

    /// <summary>Gets the requested culture (null for invariant).</summary>
    [Description("The requested culture (null for invariant).")]
    public string? Culture { get; init; }

    /// <summary>Gets the cultures this item is published in.</summary>
    [Description("The cultures this item is published in.")]
    public string[] AvailableCultures { get; init; } = [];

    /// <summary>Gets creation timestamp (UTC).</summary>
    [Description("The creation timestamp (UTC).")]
    public DateTime CreateDate { get; init; }

    /// <summary>Gets the last-edited timestamp (UTC).</summary>
    [Description("The last-edited timestamp (UTC).")]
    public DateTime UpdateDate { get; init; }

    /// <summary>
    /// Gets the key of the user who created the content item, if resolvable.
    /// Null if the user has been deleted.
    /// </summary>
    [Description("The key of the user who created the content item, if resolvable. Null if the user has been deleted.")]
    public Guid? CreatorKey { get; init; }

    /// <summary>
    /// Gets the key of the user who last edited the content item, if resolvable.
    /// Null if the user has been deleted.
    /// </summary>
    [Description("The key of the user who last edited the content item, if resolvable. Null if the user has been deleted.")]
    public Guid? WriterKey { get; init; }

    /// <summary>
    /// Gets the property values keyed by property alias, normalised for binding
    /// access. See <see cref="Cms.IContentValueNormaliser"/> for normalisation rules.
    /// </summary>
    [Description("The property values keyed by property alias, normalised for binding access.")]
    public IDictionary<string, object?> Properties { get; init; }
        = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
