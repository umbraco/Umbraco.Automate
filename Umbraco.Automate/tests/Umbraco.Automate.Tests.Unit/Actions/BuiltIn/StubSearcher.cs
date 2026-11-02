using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Search.Querying.Sorting;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

/// <summary>
/// An <see cref="ISearcher"/> that returns a fixed, ordered list of hits and honours skip/take,
/// recording each call so tests can assert on the query the action built.
/// </summary>
internal sealed class StubSearcher(IReadOnlyList<Guid> hits, UmbracoObjectTypes objectType) : ISearcher
{
    public List<SearchCall> Calls { get; } = [];

    public Task<SearchResult> SearchAsync(
        string indexAlias,
        string? query = null,
        IEnumerable<Filter>? filters = null,
        IEnumerable<Facet>? facets = null,
        IEnumerable<Sorter>? sorters = null,
        string? culture = null,
        string? segment = null,
        AccessContext? accessContext = null,
        int skip = 0,
        int take = 10,
        int maxSuggestions = 0)
    {
        Calls.Add(new SearchCall(indexAlias, query, filters?.ToList() ?? [], culture, accessContext, skip, take));

        var page = hits.Skip(skip).Take(take).Select(id => new Document(id, objectType)).ToList();
        return Task.FromResult(new SearchResult(hits.Count, page, []));
    }

    internal sealed record SearchCall(
        string IndexAlias,
        string? Query,
        IReadOnlyList<Filter> Filters,
        string? Culture,
        AccessContext? AccessContext,
        int Skip,
        int Take);
}
