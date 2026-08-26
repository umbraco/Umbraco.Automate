using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="UpdateContentPropertyAction"/>.
/// </summary>
public sealed class UpdateContentPropertySettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the content item to update.
    /// </summary>
    [Field(
        Label = "Content Key",
        Description = "The content item to update.",
        EditorUiAlias = Constants.EditorUiAliases.ContentKeyPicker,
        SupportsBindings = true)]
    public string ContentKey { get; set; } = string.Empty;

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
    /// Gets or sets the culture to write for variant content. Leave blank for invariant.
    /// </summary>
    [Field(
        Label = "Culture",
        Description = "Culture code (e.g. en-US) for variant content. Leave blank for invariant.",
        SupportsBindings = true,
        SortOrder = 3)]
    public string? Culture { get; set; }

    /// <summary>
    /// Gets or sets the segment to write for segmented content. Leave blank to target the default segment.
    /// </summary>
    [Field(
        Label = "Segment",
        Description = "Segment for segmented content. Leave blank for the default segment.",
        SupportsBindings = true,
        SortOrder = 4)]
    public string? Segment { get; set; }
}
