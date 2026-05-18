namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="MediaDeletedTrigger"/> for each deleted media item.
/// </summary>
public sealed class MediaDeletedTriggerOutput
{
    /// <summary>
    /// Gets the media item's unique key.
    /// </summary>
    public Guid MediaKey { get; init; }

    /// <summary>
    /// Gets the media item's name.
    /// </summary>
    public string? MediaName { get; init; }

    /// <summary>
    /// Gets the media type's unique key.
    /// </summary>
    public Guid? MediaTypeKey { get; init; }

    /// <summary>
    /// Gets the media type alias.
    /// </summary>
    public string? MediaTypeAlias { get; init; }
}
