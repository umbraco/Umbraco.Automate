using Umbraco.Automate.Core.Dispatch.Authorization;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="MediaSavedTrigger"/> for each saved media item.
/// </summary>
public sealed class MediaSavedTriggerOutput : IMediaScopedTriggerOutput
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
    /// Gets a value indicating whether this save represents a newly-created media item.
    /// True when <c>CreateDate == UpdateDate</c> on the saved entity. Soft signal —
    /// database date precision may vary, so downstream automations needing a hard
    /// guarantee should re-fetch.
    /// </summary>
    public bool IsNew { get; init; }

    Guid? IMediaScopedTriggerOutput.GetMediaKey() => MediaKey;
}
