namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A single match returned by <see cref="FindMediaAction"/>. Intentionally slim —
/// no property values — so that large result sets stay cheap to serialise through the
/// outbox. Downstream steps needing full media data can call <see cref="GetMediaAction"/>
/// with the <see cref="MediaKey"/>.
/// </summary>
public sealed class FindMediaMatch
{
    /// <summary>Gets the media item's unique key.</summary>
    public Guid MediaKey { get; init; }

    /// <summary>Gets the media item's name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the media type alias.</summary>
    public string MediaTypeAlias { get; init; } = string.Empty;

    /// <summary>Gets the parent media key, if available in the index.</summary>
    public Guid? ParentKey { get; init; }

    /// <summary>Gets the tree level (1 = root). Zero if unavailable from the index.</summary>
    public int Level { get; init; }

    /// <summary>Gets the node path (comma-separated IDs, CMS internal format).</summary>
    public string? Path { get; init; }

    /// <summary>
    /// Gets the URL for the media file, resolved from its <c>umbracoFile</c> property.
    /// Null if the media type has no such property or the file can't be resolved.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>Gets the creation timestamp (UTC). Default (0001-01-01) if unavailable.</summary>
    public DateTime CreateDate { get; init; }

    /// <summary>Gets the last-edited timestamp (UTC). Default (0001-01-01) if unavailable.</summary>
    public DateTime UpdateDate { get; init; }
}
