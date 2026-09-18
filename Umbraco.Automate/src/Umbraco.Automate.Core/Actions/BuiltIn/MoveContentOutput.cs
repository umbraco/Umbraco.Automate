namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="MoveContentAction"/>.
/// </summary>
public sealed class MoveContentOutput
{
    /// <summary>
    /// Gets the key of the content item that was moved.
    /// </summary>
    public Guid ContentKey { get; init; }

    /// <summary>
    /// Gets the key of the new parent, or <c>null</c> when the content was moved to the root.
    /// </summary>
    public Guid? ParentKey { get; init; }
}
