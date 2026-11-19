using Json.Schema.Generation;
using Umbraco.Automate.Core.Dispatch.Authorization;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="ElementPublishedTrigger"/> for each published Library element.
/// </summary>
public sealed class ElementPublishedTriggerOutput : IElementScopedTriggerOutput
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
    /// Gets the cultures published in this event. Null for invariant elements.
    /// Reports the cultures that changed where that is determinable, and falls back to all
    /// currently-published cultures otherwise.
    /// </summary>
    [Description("The cultures published in this event. Null for invariant elements.")]
    public string[]? Cultures { get; init; }

    Guid? IElementScopedTriggerOutput.GetElementKey() => ElementKey;
}
