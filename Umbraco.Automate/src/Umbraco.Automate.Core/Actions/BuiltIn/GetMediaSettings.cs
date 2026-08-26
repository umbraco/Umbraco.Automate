using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="GetMediaAction"/>.
/// </summary>
public sealed class GetMediaSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the media item to fetch. Typically bound
    /// from a trigger output (e.g. <c>{{ trigger.mediaKey }}</c>).
    /// </summary>
    [Field(
        Label = "Media Key",
        Description = "The key of the media item to fetch.",
        SupportsBindings = true)]
    public string MediaKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the culture to read property values for. Leave blank for invariant media.
    /// </summary>
    [Field(
        Label = "Culture",
        Description = "Culture code (e.g. en-US) for variant media. Leave blank for invariant.",
        SupportsBindings = true,
        SortOrder = 1)]
    public string? Culture { get; set; }
}
