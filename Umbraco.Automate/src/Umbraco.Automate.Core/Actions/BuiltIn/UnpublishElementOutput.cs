using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="UnpublishElementAction"/>.
/// </summary>
public sealed class UnpublishElementOutput
{
    /// <summary>
    /// Gets the key of the Library element that was unpublished.
    /// </summary>
    [Description("The key of the Library element that was unpublished.")]
    public Guid ElementKey { get; init; }

    /// <summary>
    /// Gets the cultures that were named for unpublishing, or null when all cultures were unpublished.
    /// </summary>
    [Description("The cultures that were named for unpublishing, or null when all cultures were unpublished.")]
    public string[]? Cultures { get; init; }
}
