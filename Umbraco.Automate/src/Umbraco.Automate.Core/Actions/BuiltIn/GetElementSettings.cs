using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="GetElementAction"/>.
/// </summary>
public sealed class GetElementSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the Library element to fetch. Typically bound
    /// from a trigger output (e.g. <c>{{ trigger.elementKey }}</c>).
    /// </summary>
    [Field(
        Label = "Element",
        Description = "The Library element to fetch.",
        EditorUiAlias = Constants.EditorUiAliases.ElementKeyPicker,
        SupportsBindings = true)]
    public string ElementKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the culture to read property values for. Leave blank for invariant elements.
    /// </summary>
    [Field(
        Label = "Culture",
        Description = "Culture code (e.g. en-US) for variant elements. Leave blank for invariant.",
        SupportsBindings = true,
        SortOrder = 1)]
    public string? Culture { get; set; }
}
