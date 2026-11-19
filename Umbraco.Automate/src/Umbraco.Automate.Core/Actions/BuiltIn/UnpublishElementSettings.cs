using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="UnpublishElementAction"/>.
/// </summary>
public sealed class UnpublishElementSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the Library element to unpublish.
    /// </summary>
    [Field(
        Label = "Element",
        Description = "The Library element to unpublish.",
        EditorUiAlias = Constants.EditorUiAliases.ElementKeyPicker,
        SupportsBindings = true)]
    public string ElementKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the culture(s) to unpublish, comma-separated. Leave empty for all cultures.
    /// </summary>
    [Field(Label = "Cultures", Description = "Comma-separated culture codes to unpublish (e.g. en-US,da-DK). Leave empty to unpublish all cultures.", SortOrder = 1)]
    public string? Cultures { get; set; }
}
