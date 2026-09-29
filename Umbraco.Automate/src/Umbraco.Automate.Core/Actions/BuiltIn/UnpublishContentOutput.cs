using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="UnpublishContentAction"/>.
/// </summary>
public sealed class UnpublishContentOutput
{
    /// <summary>
    /// Gets the key of the content item that was unpublished.
    /// </summary>
    [Description("The key of the content item that was unpublished.")]
    public Guid ContentKey { get; init; }

    /// <summary>
    /// Gets the cultures that were unpublished, or null for invariant content.
    /// </summary>
    [Description("The cultures that were unpublished, or null for invariant content.")]
    public string[]? Cultures { get; init; }
}
