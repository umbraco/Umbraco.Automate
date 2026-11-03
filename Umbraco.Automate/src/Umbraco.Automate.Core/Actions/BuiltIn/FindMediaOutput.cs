using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by <see cref="FindMediaAction"/>.
/// </summary>
public sealed class FindMediaOutput
{
    /// <summary>Gets the matching media items, ordered by relevance score.</summary>
    [Description("The matching media items, ordered by relevance score.")]
    public IReadOnlyList<FindMediaMatch> Matches { get; init; } = [];

    /// <summary>Gets the number of matches in this result (may equal <c>Limit</c> — see <see cref="LimitReached"/>).</summary>
    [Description("The number of matches in this result.")]
    public int Count => Matches.Count;

    /// <summary>
    /// Gets the first match, if any. Convenience for automations that expect a single hit —
    /// downstream bindings can read <c>{{ step.output.first.mediaKey }}</c> without a loop.
    /// </summary>
    [Description("The first match, if any — a convenience for automations that expect a single hit.")]
    public FindMediaMatch? First => Matches.Count > 0 ? Matches[0] : null;

    /// <summary>
    /// Gets a value indicating whether the match count equals the configured <c>Limit</c>.
    /// True means more matches may exist in the index — tighten the query or raise the limit. It is also true when the
    /// search stopped at its ceiling of examined results (<c>FindByNameSearch.MaxHitsExamined</c>) before reaching the limit.
    /// </summary>
    [Description("Whether the match count reached the configured limit, or the search stopped at its ceiling of 2000 examined results — true means more matches may exist.")]
    public bool LimitReached { get; init; }
}
