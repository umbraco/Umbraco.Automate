using Umbraco.Automate.Core.Dispatch.Authorization;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="ContentSavedTrigger"/> for each saved content item.
/// </summary>
public sealed class ContentSavedTriggerOutput : IContentScopedTriggerOutput
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
    /// Gets a value indicating whether this save represents a newly-created content item.
    /// True when <c>CreateDate == UpdateDate</c> on the saved entity. Soft signal —
    /// database date precision may vary, so downstream automations needing a hard
    /// guarantee should re-fetch.
    /// </summary>
    public bool IsNew { get; init; }

    /// <summary>
    /// Gets the cultures whose variant data changed in this save.
    /// Null for invariant content; an empty array indicates a variant content
    /// save where no culture variants were dirty (rare, but possible).
    /// </summary>
    public string[]? Cultures { get; init; }

    Guid? IContentScopedTriggerOutput.GetContentKey() => ContentKey;
}
