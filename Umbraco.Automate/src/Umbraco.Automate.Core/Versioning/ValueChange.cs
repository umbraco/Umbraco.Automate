namespace Umbraco.Automate.Core.Versioning;

/// <summary>
/// Represents a single value change between two versions of an entity.
/// Used for version comparison and diff visualization.
/// </summary>
public sealed class ValueChange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValueChange"/> class.
    /// </summary>
    /// <param name="path">The property path of the changed value.</param>
    /// <param name="oldValue">The old value (before the change).</param>
    /// <param name="newValue">The new value (after the change).</param>
    public ValueChange(string path, string? oldValue, string? newValue)
    {
        Path = path;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>
    /// Gets the property path of the changed value.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the old value (before the change).
    /// </summary>
    public string? OldValue { get; }

    /// <summary>
    /// Gets the new value (after the change).
    /// </summary>
    public string? NewValue { get; }
}
