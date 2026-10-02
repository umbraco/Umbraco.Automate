using Json.Schema.Generation;
using Umbraco.Automate.Core.Dispatch.Authorization;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="MediaTrashedTrigger"/> for each trashed media item.
/// </summary>
public sealed class MediaTrashedTriggerOutput : IMediaScopedTriggerOutput
{
    /// <summary>
    /// Gets the media item's unique key.
    /// </summary>
    [Description("The media item's unique key.")]
    public Guid MediaKey { get; init; }

    /// <summary>
    /// Gets the media item's name.
    /// </summary>
    [Description("The media item's name.")]
    public string? MediaName { get; init; }

    /// <summary>
    /// Gets the media type's unique key.
    /// </summary>
    [Description("The media type's unique key.")]
    public Guid? MediaTypeKey { get; init; }

    /// <summary>
    /// Gets the media type alias.
    /// </summary>
    [Description("The media type alias.")]
    public string? MediaTypeAlias { get; init; }

    /// <summary>
    /// Gets the path the media item occupied before it was moved to the recycle bin —
    /// useful for cleanup automations that need to know the original location.
    /// </summary>
    [Description("The path the media item occupied before it was moved to the recycle bin.")]
    public string? OriginalPath { get; init; }

    Guid? IMediaScopedTriggerOutput.GetMediaKey() => MediaKey;
}
