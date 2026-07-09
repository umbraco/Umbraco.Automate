using Umbraco.Automate.Core.Dispatch.Authorization;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="ContentPublishedTrigger"/> for each published content item.
/// </summary>
public sealed class ContentPublishedTriggerOutput : IContentScopedTriggerOutput
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
    /// Gets the cultures published in this event. Null for invariant content.
    /// Reports the cultures that changed where that is determinable, and falls back to all
    /// currently-published cultures otherwise (e.g. bulk/branch publishes).
    /// </summary>
    public string[]? Cultures { get; init; }

    Guid? IContentScopedTriggerOutput.GetContentKey() => ContentKey;
}
