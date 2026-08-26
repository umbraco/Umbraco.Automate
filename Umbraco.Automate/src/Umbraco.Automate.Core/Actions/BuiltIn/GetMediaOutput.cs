namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="GetMediaAction"/>.
/// </summary>
public sealed class GetMediaOutput
{
    /// <summary>Gets the media item's unique key.</summary>
    public Guid MediaKey { get; init; }

    /// <summary>Gets the media item's name in the requested culture, or its invariant name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the media type alias.</summary>
    public string? MediaTypeAlias { get; init; }

    /// <summary>Gets the media type key.</summary>
    public Guid MediaTypeKey { get; init; }

    /// <summary>Gets the parent media key, if any.</summary>
    public Guid? ParentKey { get; init; }

    /// <summary>Gets the tree level (1 = root).</summary>
    public int Level { get; init; }

    /// <summary>Gets the node path (comma-separated IDs, CMS internal format).</summary>
    public string? Path { get; init; }

    /// <summary>Gets the sort order among siblings.</summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// Gets the URL for the media file, resolved from its <c>umbracoFile</c> property.
    /// Null if the media type has no such property or the file can't be resolved.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>Gets the requested culture (null for invariant).</summary>
    public string? Culture { get; init; }

    /// <summary>Gets the cultures this item is available in.</summary>
    public string[] AvailableCultures { get; init; } = [];

    /// <summary>Gets creation timestamp (UTC).</summary>
    public DateTime CreateDate { get; init; }

    /// <summary>Gets the last-edited timestamp (UTC).</summary>
    public DateTime UpdateDate { get; init; }

    /// <summary>
    /// Gets the key of the user who created the media item, if resolvable.
    /// Null if the user has been deleted.
    /// </summary>
    public Guid? CreatorKey { get; init; }

    /// <summary>
    /// Gets the key of the user who last edited the media item, if resolvable.
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
