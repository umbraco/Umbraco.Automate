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
    /// Gets the cultures that were published in this event.
    /// Null for invariant content; an empty array indicates a variant content
    /// publish where no culture variants were dirty (rare, but possible).
    /// </summary>
    public string[]? Cultures { get; init; }

    Guid? IContentScopedTriggerOutput.GetContentKey() => ContentKey;
}
