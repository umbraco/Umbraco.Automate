namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="CreateContentAction"/>.
/// </summary>
public sealed class CreateContentOutput
{
    /// <summary>Gets the key of the created content item. <see cref="Guid.Empty"/> when creation did not happen.</summary>
    public Guid ContentKey { get; init; }

    /// <summary>Gets the name that was requested for the new content item.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the content type alias that was requested.</summary>
    public string ContentTypeAlias { get; init; } = string.Empty;

    /// <summary>Gets the key of the parent content item the new item was created under.</summary>
    public Guid ParentKey { get; init; }
}
