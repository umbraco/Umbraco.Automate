using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="PublishContentAction"/>.
/// </summary>
public sealed class PublishContentOutput
{
    /// <summary>
    /// Gets the key of the content item that was published.
    /// </summary>
    [Description("The key of the content item that was published.")]
    public Guid ContentKey { get; init; }

    /// <summary>
    /// Gets the cultures that were published.
    /// </summary>
    [Description("The cultures that were published.")]
    public string?[] Cultures { get; init; } = [];
}
