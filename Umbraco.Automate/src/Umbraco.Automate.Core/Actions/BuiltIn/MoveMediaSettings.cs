using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="MoveMediaAction"/>.
/// </summary>
public sealed class MoveMediaSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the media item to move.
    /// </summary>
    [Field(Label = "Media Key", Description = "The key of the media item to move.", SupportsBindings = true)]
    public string MediaKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key (GUID) of the parent to move the media item under. Leave empty to
    /// move the media item to the root.
    /// </summary>
    [Field(
        Label = "Target Parent Key",
        Description = "The key of the parent to move the media item under. Leave empty to move to the root.",
        SupportsBindings = true,
        SortOrder = 1)]
    public string? TargetParentKey { get; set; }
}
