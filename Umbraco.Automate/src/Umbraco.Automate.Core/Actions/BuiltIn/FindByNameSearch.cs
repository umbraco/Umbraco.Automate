using Umbraco.Cms.Core.Extensions;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Search.Querying.Sorting;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>The outcome of a <see cref="FindByNameSearch"/>.</summary>
/// <param name="Matches">The matches, in relevance order, at most <c>limit</c> of them.</param>
/// <param name="LimitReached">Whether <c>limit</c> matches were found, so more may exist.</param>
/// <param name="HitCeilingReached">Whether the search stopped at the hit ceiling before finding <c>limit</c> matches, so more may exist.</param>
internal sealed record FindByNameSearchResult<TMatch>(
    IReadOnlyList<TMatch> Matches,
    bool LimitReached,
    bool HitCeilingReached)
{
    /// <summary>Gets a value indicating whether more matches may exist than were returned.</summary>
    public bool MoreMayExist => LimitReached || HitCeilingReached;
}

/// <summary>
/// The name search shared by Find Content and Find Media, built on Umbraco Search
/// (<see cref="ISearcher"/>) rather than on a provider's native query syntax.
/// </summary>
/// <remarks>
/// Umbraco Search can only express whole-word and word-prefix matches on the analysed name, and its
/// analyzer keeps no token for stop words or symbols and splits words on punctuation. So the search
/// only narrows candidates (every term the index can hold must start a word), and every match mode
/// then checks the item's real name. A search hit carries only the item key, so the caller loads
/// each hit's details.
/// </remarks>
internal static class FindByNameSearch
{
    /// <summary>Upper bound on the number of search hits examined for one run.</summary>
    internal const int MaxHitsExamined = 2000;

    /// <summary>The smallest number of hits asked for per search call.</summary>
    internal const int MinPageSize = 100;

    private static readonly char[] LeadingWildcards = ['*', '?'];

    /// <summary>
    /// The words the index's analyzer drops. The Examine provider indexes names with Examine's
    /// full-text analyzer, Lucene's StandardAnalyzer, whose default stop set is Lucene.Net 4.8
    /// <c>StopAnalyzer.ENGLISH_STOP_WORDS_SET</c>. Core can't reference Lucene, so the set is copied;
    /// an integration test compares it with Lucene's.
    /// </summary>
    internal static readonly IReadOnlySet<string> IndexStopWords = new HashSet<string>(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "but", "by", "for", "if", "in", "into", "is", "it",
        "no", "not", "of", "on", "or", "such", "that", "the", "their", "then", "there", "these", "they",
        "this", "to", "was", "will", "with",
    };

    /// <summary>
    /// Splits a name into the lower-case terms the index is queried with: whitespace-separated,
    /// leading wildcard characters removed (a wildcard query can't start with one), empty terms
    /// dropped. Punctuation inside a term stays, because the index keeps it inside a word
    /// (<c>children's</c>, <c>2.0</c>). Lower-casing is needed because wildcard clauses skip the analyzer.
    /// </summary>
    internal static string[] GetTerms(string name)
        => name
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(term => term.TrimStart(LeadingWildcards).ToLowerInvariant())
            .Where(term => term.Length > 0)
            .ToArray();

    /// <summary>
    /// Whether the index can hold a token for <paramref name="term"/>: it needs a letter or digit
    /// outside the stop words. A symbol like <c>&amp;</c> or a stop word like <c>the</c> can't be
    /// searched for, so requiring it in the query would find nothing; the name check covers it.
    /// </summary>
    internal static bool IsIndexable(string term)
    {
        var pieces = SplitOnPunctuation(term);
        return pieces.Any(piece => !IndexStopWords.Contains(piece));
    }

    /// <summary>
    /// Builds the search filters (all ANDed): one text filter per indexable term of
    /// <paramref name="name"/>, an optional filter on the type keys (ORed), and one that leaves out
    /// items in the recycle bin. A name with no indexable term adds no text filter, so the search
    /// is then the type, trash and culture constraints alone and the name check does all the work.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="typeKeys">The content or media type keys to restrict to; empty for all types.</param>
    /// <param name="recycleBinKey">The key of the recycle bin whose items are excluded.</param>
    internal static IReadOnlyList<Filter> BuildFilters(string name, IReadOnlyCollection<Guid> typeKeys, Guid recycleBinKey)
    {
        var filters = GetTerms(name)
            .Where(IsIndexable)
            .Select(term => (Filter)new TextFilter(UmbracoConstants.Search.FieldNames.Name, [term], Negate: false))
            .ToList();

        if (typeKeys.Count > 0)
        {
            filters.Add(new KeywordFilter(
                UmbracoConstants.Search.FieldNames.ContentTypeId,
                typeKeys.Select(key => key.AsKeyword()).ToArray(),
                Negate: false));
        }

        // The same exclusion the CMS's own entity search uses: trashed items carry the recycle bin in their path.
        filters.Add(new KeywordFilter(
            UmbracoConstants.Search.FieldNames.PathIds,
            [recycleBinKey.AsKeyword()],
            Negate: true));

        return filters;
    }

    /// <summary>Parses the CSV of type keys a type picker stores, skipping anything that isn't a key.</summary>
    internal static IReadOnlyList<Guid> ParseKeys(string? keysCsv)
    {
        if (string.IsNullOrWhiteSpace(keysCsv))
        {
            return [];
        }

        return keysCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => Guid.TryParse(part, out var key) ? (Guid?)key : null)
            .Where(key => key.HasValue)
            .Select(key => key!.Value)
            .ToArray();
    }

    /// <summary>
    /// Checks an item's name against the value, ignoring case and runs of whitespace:
    /// Exact is the whole name and Contains is the value as one run inside the name, both compared
    /// as typed. StartsWith needs every term to start a word of the name: a term without punctuation
    /// may start any run of letters and digits (the index splits <c>Hallo-World</c> into two words,
    /// so <c>world</c> finds it), while a term with punctuation (<c>c++</c>, <c>q&amp;a</c>) must
    /// start a whitespace-separated word, as the index can't tell what it was attached to.
    /// </summary>
    internal static bool IsNameMatch(string? itemName, string name, FindContentMatchMode mode)
    {
        if (itemName is null)
        {
            return false;
        }

        var normalisedName = CollapseWhitespace(itemName);
        var normalisedValue = CollapseWhitespace(name);

        return mode switch
        {
            FindContentMatchMode.StartsWith => StartsWordsOf(normalisedName, GetTerms(name)),
            FindContentMatchMode.Contains => normalisedName.Contains(normalisedValue, StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(normalisedName, normalisedValue, StringComparison.OrdinalIgnoreCase),
        };
    }

    private static string CollapseWhitespace(string text)
        => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static bool StartsWordsOf(string normalisedName, string[] terms)
    {
        var words = normalisedName.Split(' ');
        var runs = SplitOnPunctuation(normalisedName);

        return terms.All(term =>
        {
            var candidates = term.All(char.IsLetterOrDigit) ? words.Concat(runs) : words;
            return candidates.Any(candidate => candidate.StartsWith(term, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static string[] SplitOnPunctuation(string term)
        => System.Text.RegularExpressions.Regex
            .Split(term, @"[^\p{L}\p{N}]+")
            .Where(piece => piece.Length > 0)
            .ToArray();

    /// <summary>
    /// Runs the search, paging through hits until <paramref name="limit"/> matches have passed the
    /// name check, the hits run out, or <see cref="MaxHitsExamined"/> hits have been examined.
    /// </summary>
    /// <param name="searcher">The searcher for <paramref name="indexAlias"/>.</param>
    /// <param name="indexAlias">The index to search.</param>
    /// <param name="name">The name value to match.</param>
    /// <param name="mode">How <paramref name="name"/> is matched against item names.</param>
    /// <param name="culture">The culture to search in (invariant items are always included), or null.</param>
    /// <param name="filters">The filters from <see cref="BuildFilters"/>.</param>
    /// <param name="limit">The maximum number of matches to return.</param>
    /// <param name="loadMatches">Loads the matches for a page of hit keys, dropping keys that no longer resolve.</param>
    /// <param name="getName">Reads the name a match is checked against.</param>
    /// <param name="cancellationToken">Checked before each page is fetched.</param>
    internal static async Task<FindByNameSearchResult<TMatch>> SearchAsync<TMatch>(
        ISearcher searcher,
        string indexAlias,
        string name,
        FindContentMatchMode mode,
        string? culture,
        IReadOnlyList<Filter> filters,
        int limit,
        Func<IReadOnlyList<Guid>, IReadOnlyList<TMatch>> loadMatches,
        Func<TMatch, string?> getName,
        CancellationToken cancellationToken)
    {
        var matches = new List<TMatch>();
        var seen = new HashSet<Guid>();
        var pageSize = Math.Max(limit, MinPageSize);
        var skip = 0;
        var hitCeilingReached = false;

        while (matches.Count < limit)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (skip >= MaxHitsExamined)
            {
                hitCeilingReached = true;
                break;
            }

            // No access context: protected content is left out, as the published index's default does.
            var page = await searcher.SearchAsync(
                indexAlias,
                query: null,
                filters: filters,
                sorters: [new ScoreSorter(Umbraco.Cms.Core.Direction.Descending)],
                culture: culture,
                accessContext: null,
                skip: skip,
                take: pageSize);

            var hits = page.Documents.ToList();
            if (hits.Count == 0)
            {
                break;
            }

            skip += hits.Count;

            var keys = hits.Select(hit => hit.Id).Where(seen.Add).ToList();
            foreach (var match in loadMatches(keys).Where(m => IsNameMatch(getName(m), name, mode)))
            {
                matches.Add(match);
                if (matches.Count == limit)
                {
                    break;
                }
            }

            if (skip >= page.Total)
            {
                break;
            }
        }

        return new FindByNameSearchResult<TMatch>(matches, matches.Count >= limit, hitCeilingReached);
    }
}
