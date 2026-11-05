using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="UpdateElementPropertyAction"/>.
/// </summary>
public sealed class UpdateElementPropertySettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the Library element to update.
    /// </summary>
    [Field(
        Label = "Element",
        Description = "The Library element to update.",
        EditorUiAlias = Constants.EditorUiAliases.ElementKeyPicker,
        SupportsBindings = true)]
    public string ElementKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the alias of the property to update.
    /// </summary>
    [Field(
        Label = "Property Alias",
        Description = "The alias of the property to update.",
        SupportsBindings = true,
        SortOrder = 1)]
    public string PropertyAlias { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the value to write. Sent as a string from the UI; the underlying
    /// Umbraco property editor determines how the value is coerced and stored.
    /// </summary>
    [Field(
        Label = "Value",
        Description = "The value to write. Must match the shape expected by the target property editor.",
        SupportsBindings = true,
        SortOrder = 2)]
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets the culture to write for a variant element. Leave blank for invariant.
    /// </summary>
    [Field(
        Label = "Culture",
        Description = "Culture code (e.g. en-US) for a variant element. Leave blank for invariant.",
        SupportsBindings = true,
        SortOrder = 3)]
    public string? Culture { get; set; }

    /// <summary>
    /// Gets or sets the segment to write for a segmented element. Leave blank to target the default segment.
    /// </summary>
    [Field(
        Label = "Segment",
        Description = "Segment for a segmented element. Leave blank for the default segment.",
        SupportsBindings = true,
        SortOrder = 4)]
    public string? Segment { get; set; }
}
