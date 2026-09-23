namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="MoveMediaAction"/>.
/// </summary>
public sealed class MoveMediaOutput
{
    /// <summary>
    /// Gets the key of the media item that was moved.
    /// </summary>
    public Guid MediaKey { get; init; }

    /// <summary>
    /// Gets the key of the new parent, or <c>null</c> when the media item was moved to the root.
    /// </summary>
    public Guid? ParentKey { get; init; }
}
