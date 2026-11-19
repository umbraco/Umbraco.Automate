using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="PublishElementAction"/>.
/// </summary>
public sealed class PublishElementSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the Library element to publish.
    /// </summary>
    [Field(
        Label = "Element",
        Description = "The Library element to publish.",
        EditorUiAlias = Constants.EditorUiAliases.ElementKeyPicker,
        SupportsBindings = true)]
    public string ElementKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the culture(s) to publish, comma-separated. Leave empty for invariant elements.
    /// </summary>
    [Field(Label = "Cultures", Description = "Comma-separated culture codes to publish (e.g. en-US,da-DK). Leave empty for invariant elements.", SortOrder = 1)]
    public string? Cultures { get; set; }
}
