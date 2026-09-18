using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="MoveContentAction"/>.
/// </summary>
public sealed class MoveContentSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the content item to move.
    /// </summary>
    [Field(Label = "Content Key", Description = "The key of the content item to move.", SupportsBindings = true)]
    public string ContentKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key (GUID) of the parent to move the content under. Leave empty to
    /// move the content to the root.
    /// </summary>
    [Field(
        Label = "Target Parent Key",
        Description = "The key of the parent to move the content under. Leave empty to move to the root.",
        SupportsBindings = true,
        SortOrder = 1)]
    public string? TargetParentKey { get; set; }
}
