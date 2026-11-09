using Json.Schema.Generation;
using Umbraco.Automate.Core.Dispatch.Authorization;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="ElementUnpublishedTrigger"/> for each unpublished Library element.
/// </summary>
public sealed class ElementUnpublishedTriggerOutput : IElementScopedTriggerOutput
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
    /// Gets the cultures that were published before the element was unpublished.
    /// Null for invariant elements (the whole element was unpublished).
    /// </summary>
    [Description("The cultures that were published before the element was unpublished. Null for invariant elements.")]
    public string[]? Cultures { get; init; }

    Guid? IElementScopedTriggerOutput.GetElementKey() => ElementKey;
}
