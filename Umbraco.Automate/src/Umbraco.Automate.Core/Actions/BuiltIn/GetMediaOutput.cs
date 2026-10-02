using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="GetMediaAction"/>.
/// </summary>
public sealed class GetMediaOutput
{
    /// <summary>Gets the media item's unique key.</summary>
    [Description("The media item's unique key.")]
    public Guid MediaKey { get; init; }

    /// <summary>Gets the media item's name in the requested culture, or its invariant name.</summary>
    [Description("The media item's name in the requested culture, or its invariant name.")]
    public string? Name { get; init; }

    /// <summary>Gets the media type alias.</summary>
    [Description("The media type alias.")]
    public string? MediaTypeAlias { get; init; }

    /// <summary>Gets the media type key.</summary>
    [Description("The media type key.")]
    public Guid MediaTypeKey { get; init; }

    /// <summary>Gets the parent media key, if any.</summary>
    [Description("The parent media key, if any.")]
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

    /// <summary>
    /// Gets the URL for the media file, resolved from its <c>umbracoFile</c> property.
    /// Null if the media type has no such property or the file can't be resolved.
    /// </summary>
    [Description("The URL for the media file. Null if the media type has no file property or it can't be resolved.")]
    public string? Url { get; init; }

    /// <summary>Gets the requested culture (null for invariant).</summary>
    [Description("The requested culture (null for invariant).")]
    public string? Culture { get; init; }

    /// <summary>Gets the cultures this item is available in.</summary>
    [Description("The cultures this item is available in.")]
    public string[] AvailableCultures { get; init; } = [];

    /// <summary>Gets creation timestamp (UTC).</summary>
    [Description("The creation timestamp (UTC).")]
    public DateTime CreateDate { get; init; }

    /// <summary>Gets the last-edited timestamp (UTC).</summary>
    [Description("The last-edited timestamp (UTC).")]
    public DateTime UpdateDate { get; init; }

    /// <summary>
    /// Gets the key of the user who created the media item, if resolvable.
    /// Null if the user has been deleted.
    /// </summary>
    [Description("The key of the user who created the media item, if resolvable. Null if the user has been deleted.")]
    public Guid? CreatorKey { get; init; }

    /// <summary>
    /// Gets the key of the user who last edited the media item, if resolvable.
    /// Null if the user has been deleted.
    /// </summary>
    [Description("The key of the user who last edited the media item, if resolvable. Null if the user has been deleted.")]
    public Guid? WriterKey { get; init; }

    /// <summary>
    /// Gets the property values keyed by property alias, normalised for binding
    /// access. See <see cref="Cms.IContentValueNormaliser"/> for normalisation rules.
    /// </summary>
    [Description("The property values keyed by property alias, normalised for binding access.")]
    public IDictionary<string, object?> Properties { get; init; }
        = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
