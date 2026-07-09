using Umbraco.Automate.Core.Dispatch.Authorization;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="ContentUnpublishedTrigger"/> for each unpublished content item.
/// </summary>
public sealed class ContentUnpublishedTriggerOutput : IContentScopedTriggerOutput
{
    /// <summary>
    /// Gets the content item's unique key.
    /// </summary>
    public Guid ContentKey { get; init; }

    /// <summary>
    /// Gets the content item's name.
    /// </summary>
    public string? ContentName { get; init; }

    /// <summary>
    /// Gets the content type's unique key.
    /// </summary>
    public Guid? ContentTypeKey { get; init; }

    /// <summary>
    /// Gets the content type alias.
    /// </summary>
    public string? ContentTypeAlias { get; init; }

    /// <summary>
    /// Gets the cultures that were published before the item was unpublished.
    /// Null for invariant content (the whole item was unpublished).
    /// </summary>
    public string[]? Cultures { get; init; }

    Guid? IContentScopedTriggerOutput.GetContentKey() => ContentKey;
}
