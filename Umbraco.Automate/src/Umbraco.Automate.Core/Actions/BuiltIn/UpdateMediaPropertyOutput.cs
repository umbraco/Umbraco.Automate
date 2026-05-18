namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="UpdateMediaPropertyAction"/>.
/// </summary>
public sealed class UpdateMediaPropertyOutput
{
    /// <summary>Gets the key of the media item that was updated.</summary>
    public Guid MediaKey { get; init; }

    /// <summary>Gets the alias of the property that was updated.</summary>
    public string PropertyAlias { get; init; } = string.Empty;

    /// <summary>Gets the culture that was targeted (null for invariant).</summary>
    public string? Culture { get; init; }

    /// <summary>Gets the segment that was targeted (null for the default segment).</summary>
    public string? Segment { get; init; }

    /// <summary>
    /// Gets the previous property value (before the update), for audit/binding purposes.
    /// </summary>
    public object? PreviousValue { get; init; }
}
