using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="PublishContentAction"/>.
/// </summary>
public sealed class PublishContentSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the content item to publish.
    /// </summary>
    [Field(
        Label = "Content Key",
        Description = "The content item to publish.",
        EditorUiAlias = Constants.EditorUiAliases.ContentKeyPicker,
        SupportsBindings = true)]
    public string ContentKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the culture(s) to publish, comma-separated. Leave empty for invariant content.
    /// </summary>
    [Field(Label = "Cultures", Description = "Comma-separated culture codes to publish (e.g. en-US,da-DK). Leave empty for invariant content.", SortOrder = 1)]
    public string? Cultures { get; set; }
}
