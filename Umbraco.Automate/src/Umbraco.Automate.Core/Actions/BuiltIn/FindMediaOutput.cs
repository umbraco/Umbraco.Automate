namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by <see cref="FindMediaAction"/>.
/// </summary>
public sealed class FindMediaOutput
{
    /// <summary>Gets the matching media items, ordered by Examine relevance score.</summary>
    public IReadOnlyList<FindMediaMatch> Matches { get; init; } = [];

    /// <summary>Gets the number of matches in this result (may equal <c>Limit</c> — see <see cref="LimitReached"/>).</summary>
    public int Count => Matches.Count;

    /// <summary>
    /// Gets the first match, if any. Convenience for automations that expect a single hit —
    /// downstream bindings can read <c>{{ step.output.first.mediaKey }}</c> without a loop.
    /// </summary>
    public FindMediaMatch? First => Matches.Count > 0 ? Matches[0] : null;

    /// <summary>
    /// Gets a value indicating whether the match count equals the configured <c>Limit</c>.
    /// True means more matches may exist in the index — tighten the query or raise the limit.
    /// </summary>
    public bool LimitReached { get; init; }
}
