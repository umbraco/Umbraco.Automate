using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="GetElementPropertyAction"/>.
/// </summary>
public sealed class GetElementPropertySettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the Library element to read from.
    /// </summary>
    [Field(
        Label = "Element",
        Description = "The Library element to read from.",
        EditorUiAlias = Constants.EditorUiAliases.ElementKeyPicker,
        SupportsBindings = true)]
    public string ElementKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the alias of the property to read.
    /// </summary>
    [Field(
        Label = "Property Alias",
        Description = "The alias of the property to read.",
        SupportsBindings = true,
        SortOrder = 1)]
    public string PropertyAlias { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the culture to read the property value for.
    /// </summary>
    [Field(
        Label = "Culture",
        Description = "Culture code (e.g. en-US) for variant elements. Leave blank for invariant.",
        SupportsBindings = true,
        SortOrder = 2)]
    public string? Culture { get; set; }
}
