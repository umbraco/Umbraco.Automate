namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="MediaTrashedTrigger"/> for each trashed media item.
/// </summary>
public sealed class MediaTrashedTriggerOutput
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

    /// <summary>
    /// Gets the path the media item occupied before it was moved to the recycle bin —
    /// useful for cleanup automations that need to know the original location.
    /// </summary>
    public string? OriginalPath { get; init; }
}
