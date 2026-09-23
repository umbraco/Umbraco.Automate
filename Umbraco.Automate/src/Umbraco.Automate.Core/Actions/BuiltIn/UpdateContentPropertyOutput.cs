using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="UpdateContentPropertyAction"/>.
/// </summary>
public sealed class UpdateContentPropertyOutput
{
    /// <summary>Gets the key of the content item that was updated.</summary>
    [Description("The key of the content item that was updated.")]
    public Guid ContentKey { get; init; }

    /// <summary>Gets the alias of the property that was updated.</summary>
    [Description("The alias of the property that was updated.")]
    public string PropertyAlias { get; init; } = string.Empty;

    /// <summary>Gets the culture that was targeted (null for invariant).</summary>
    [Description("The culture that was targeted (null for invariant).")]
    public string? Culture { get; init; }

    /// <summary>Gets the segment that was targeted (null for the default segment).</summary>
    [Description("The segment that was targeted (null for the default segment).")]
    public string? Segment { get; init; }

    /// <summary>
    /// Gets the previous property value (before the update), for audit/binding purposes.
    /// </summary>
    [Description("The previous property value, before the update.")]
    public object? PreviousValue { get; init; }
}
