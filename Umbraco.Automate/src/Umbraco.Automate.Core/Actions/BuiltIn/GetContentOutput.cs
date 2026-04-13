namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="GetContentAction"/>.
/// </summary>
public sealed class GetContentOutput
{
    /// <summary>Gets the content item's unique key.</summary>
    public Guid ContentKey { get; init; }

    /// <summary>Gets the content item's name in the requested culture, or its invariant name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the content type alias.</summary>
    public string? ContentTypeAlias { get; init; }

    /// <summary>Gets the content type key.</summary>
    public Guid ContentTypeKey { get; init; }

    /// <summary>Gets the parent content key, if any.</summary>
    public Guid? ParentKey { get; init; }

    /// <summary>Gets the tree level (1 = root).</summary>
    public int Level { get; init; }

    /// <summary>Gets the node path (comma-separated IDs, CMS internal format).</summary>
    public string? Path { get; init; }

    /// <summary>Gets the sort order among siblings.</summary>
    public int SortOrder { get; init; }

    /// <summary>Gets the URL for the content item in the requested culture.</summary>
    public string? Url { get; init; }

    /// <summary>Gets the requested culture (null for invariant).</summary>
    public string? Culture { get; init; }

    /// <summary>Gets the cultures this item is published in.</summary>
    public string[] AvailableCultures { get; init; } = [];

    /// <summary>Gets creation timestamp (UTC).</summary>
    public DateTime CreateDate { get; init; }

    /// <summary>Gets the last-edited timestamp (UTC).</summary>
    public DateTime UpdateDate { get; init; }

    /// <summary>
    /// Gets the key of the user who created the content item, if resolvable.
    /// Null if the user has been deleted.
    /// </summary>
    public Guid? CreatorKey { get; init; }

    /// <summary>
    /// Gets the key of the user who last edited the content item, if resolvable.
    /// Null if the user has been deleted.
    /// </summary>
    public Guid? WriterKey { get; init; }

    /// <summary>
    /// Gets the property values keyed by property alias, normalised for binding
    /// access. See <see cref="Cms.IContentValueNormaliser"/> for normalisation rules.
    /// </summary>
    public IDictionary<string, object?> Properties { get; init; }
        = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
