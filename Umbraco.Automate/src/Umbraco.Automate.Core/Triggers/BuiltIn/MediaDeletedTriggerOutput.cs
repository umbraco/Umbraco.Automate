using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="MediaDeletedTrigger"/> for each deleted media item.
/// </summary>
public sealed class MediaDeletedTriggerOutput
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
}
