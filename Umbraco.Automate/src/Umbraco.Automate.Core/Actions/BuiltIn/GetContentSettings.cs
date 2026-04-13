using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="GetContentAction"/>.
/// </summary>
public sealed class GetContentSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the content item to fetch. Typically bound
    /// from a trigger output (e.g. <c>{{ trigger.contentKey }}</c>).
    /// </summary>
    [Field(
        Label = "Content Key",
        Description = "The key of the content item to fetch.",
        SupportsBindings = true)]
    public string ContentKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the culture to read property values for. Leave blank for invariant content.
    /// </summary>
    [Field(
        Label = "Culture",
        Description = "Culture code (e.g. en-US) for variant content. Leave blank for invariant.",
        SupportsBindings = true,
        SortOrder = 1)]
    public string? Culture { get; set; }
}
