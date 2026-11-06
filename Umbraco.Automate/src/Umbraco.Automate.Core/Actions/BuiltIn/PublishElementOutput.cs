using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="PublishElementAction"/>.
/// </summary>
public sealed class PublishElementOutput
{
    /// <summary>
    /// Gets the key of the Library element that was published.
    /// </summary>
    [Description("The key of the Library element that was published.")]
    public Guid ElementKey { get; init; }

    /// <summary>
    /// Gets the cultures that were published.
    /// </summary>
    [Description("The cultures that were published.")]
    public string?[] Cultures { get; init; } = [];
}
