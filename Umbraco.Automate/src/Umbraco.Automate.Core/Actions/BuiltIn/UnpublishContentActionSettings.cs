using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="UnpublishContentAction"/>.
/// </summary>
public sealed class UnpublishContentActionSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the content item to unpublish. Supports binding syntax.
    /// </summary>
    [Field(Label = "Content Key", Description = "The key of the content item to unpublish. Supports ${ binding } syntax.", SupportsBindings = true)]
    public string ContentKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the culture(s) to unpublish, comma-separated. Leave empty for invariant content.
    /// </summary>
    [Field(Label = "Cultures", Description = "Comma-separated culture codes to unpublish (e.g. en-US,da-DK). Leave empty for invariant content.", SortOrder = 1)]
    public string? Cultures { get; set; }
}
