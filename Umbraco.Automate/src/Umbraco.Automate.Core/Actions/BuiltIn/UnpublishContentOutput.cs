namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="UnpublishContentAction"/>.
/// </summary>
public sealed class UnpublishContentOutput
{
    /// <summary>
    /// Gets the key of the content item that was unpublished.
    /// </summary>
    public Guid ContentKey { get; init; }

    /// <summary>
    /// Gets the cultures that were unpublished, or null for invariant content.
    /// </summary>
    public string[]? Cultures { get; init; }
}
