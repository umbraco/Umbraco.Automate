namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="CreateElementAction"/>.
/// </summary>
public sealed class CreateElementOutput
{
    /// <summary>Gets the key of the created Library element. <see cref="Guid.Empty"/> when creation did not happen.</summary>
    public Guid ElementKey { get; init; }

    /// <summary>Gets the name that was requested for the new element.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the key of the element type that was requested.</summary>
    public Guid ContentTypeKey { get; init; }

    /// <summary>
    /// Gets the alias of the element type that was requested. Empty when the key did not
    /// resolve to a real element type.
    /// </summary>
    public string ContentTypeAlias { get; init; } = string.Empty;

    /// <summary>Gets the key of the folder the element was created in, or <c>null</c> for the Library root.</summary>
    public Guid? FolderKey { get; init; }
}
