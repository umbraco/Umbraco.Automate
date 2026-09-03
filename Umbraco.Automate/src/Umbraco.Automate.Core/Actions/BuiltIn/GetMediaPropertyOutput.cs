namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="GetMediaPropertyAction"/>.
/// </summary>
public sealed class GetMediaPropertyOutput
{
    /// <summary>Gets the key of the media item that was read from.</summary>
    public Guid MediaKey { get; init; }

    /// <summary>Gets the alias of the property that was requested.</summary>
    public string PropertyAlias { get; init; } = string.Empty;

    /// <summary>Gets the culture that was requested (null for invariant).</summary>
    public string? Culture { get; init; }

    /// <summary>
    /// Gets the normalised property value. Null if the property doesn't exist on the
    /// media type, has no value, or the media item wasn't found.
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// Gets a value indicating whether a non-null property value was resolved.
    /// </summary>
    public bool HasValue { get; init; }
}
