namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="PublishContentAction"/>.
/// </summary>
public sealed class PublishContentOutput
{
    /// <summary>
    /// Gets the key of the content item that was published.
    /// </summary>
    public Guid ContentKey { get; init; }

    /// <summary>
    /// Gets the cultures that were published.
    /// </summary>
    public string?[] Cultures { get; init; } = [];
}
