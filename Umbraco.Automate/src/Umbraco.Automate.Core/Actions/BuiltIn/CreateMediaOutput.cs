namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="CreateMediaAction"/>.
/// </summary>
public sealed class CreateMediaOutput
{
    /// <summary>Gets the key of the created media item. <see cref="Guid.Empty"/> when creation did not happen.</summary>
    public Guid MediaKey { get; init; }

    /// <summary>Gets the name that was requested for the new media item.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the key of the media type that was requested.</summary>
    public Guid MediaTypeKey { get; init; }

    /// <summary>
    /// Gets the alias of the media type that was requested. Empty when the key did not
    /// resolve to a real media type.
    /// </summary>
    public string MediaTypeAlias { get; init; } = string.Empty;

    /// <summary>Gets the key of the parent media item the new item was created under.</summary>
    public Guid ParentKey { get; init; }

    /// <summary>
    /// Gets the name of the file stored on the item. Null when no source URL was configured,
    /// or when the download failed.
    /// </summary>
    public string? FileName { get; init; }
}
