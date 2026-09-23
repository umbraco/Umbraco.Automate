using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="GetContentPropertyAction"/>.
/// </summary>
public sealed class GetContentPropertyOutput
{
    /// <summary>Gets the key of the content item that was read from.</summary>
    [Description("The key of the content item that was read from.")]
    public Guid ContentKey { get; init; }

    /// <summary>Gets the alias of the property that was requested.</summary>
    [Description("The alias of the property that was requested.")]
    public string PropertyAlias { get; init; } = string.Empty;

    /// <summary>Gets the culture that was requested (null for invariant).</summary>
    [Description("The culture that was requested (null for invariant).")]
    public string? Culture { get; init; }

    /// <summary>
    /// Gets the normalised property value. Null if the property doesn't exist on the
    /// content type, has no value, or the content wasn't found.
    /// </summary>
    [Description("The normalised property value. Null if the property doesn't exist, has no value, or the content wasn't found.")]
    public object? Value { get; init; }

    /// <summary>
    /// Gets a value indicating whether a non-null property value was resolved.
    /// </summary>
    [Description("Whether a non-null property value was resolved.")]
    public bool HasValue { get; init; }
}
