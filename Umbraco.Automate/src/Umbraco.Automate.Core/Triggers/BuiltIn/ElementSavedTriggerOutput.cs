using Json.Schema.Generation;
using Umbraco.Automate.Core.Dispatch.Authorization;
using Umbraco.Cms.Core.Models;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="ElementSavedTrigger"/> for each saved Library element.
/// </summary>
public sealed class ElementSavedTriggerOutput : IElementScopedTriggerOutput
{
    /// <summary>
    /// Gets the element's unique key.
    /// </summary>
    [Description("The element's unique key.")]
    public Guid ElementKey { get; init; }

    /// <summary>
    /// Gets the element's name.
    /// </summary>
    [Description("The element's name.")]
    public string? ElementName { get; init; }

    /// <summary>
    /// Gets the element type's unique key.
    /// </summary>
    [Description("The element type's unique key.")]
    public Guid? ContentTypeKey { get; init; }

    /// <summary>
    /// Gets the element type alias.
    /// </summary>
    [Description("The element type alias.")]
    public string? ContentTypeAlias { get; init; }

    /// <summary>
    /// Gets a value indicating whether this save represents a newly-created element.
    /// True when <c>CreateDate == UpdateDate</c> on the saved entity. Soft signal —
    /// database date precision may vary, so downstream automations needing a hard
    /// guarantee should re-fetch.
    /// </summary>
    [Description("Whether this save represents a newly-created element.")]
    public bool IsNew { get; init; }

    /// <summary>
    /// Gets the cultures edited in this save. Null for invariant elements.
    /// Reports the cultures that changed where that is determinable, and falls back to all
    /// available cultures otherwise (e.g. a cloned instance).
    /// </summary>
    [Description("The cultures edited in this save. Null for invariant elements.")]
    public string[]? Cultures { get; init; }

    Guid? IElementScopedTriggerOutput.GetElementKey() => ElementKey;
}
