using Examine;
using Examine.Lucene;
using Examine.Lucene.Directories;
using Examine.Lucene.Providers;
using Examine.Search;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions.BuiltIn;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

/// <summary>
/// Runtime tests for <see cref="FindContentAction"/> queries. Unlike <see cref="FindContentActionTests"/>
/// (which only assert the generated query string), these execute the query against a real in-memory
/// Examine/Lucene index so they catch parser-level failures such as issue #90, where the "Contains"
/// mode emits a leading-wildcard term (<c>nodeName:*term*</c>) that Lucene rejects by default.
/// </summary>
public sealed class FindContentQueryExecutionTests : IDisposable
{
    private readonly LuceneIndex _index;

    public FindContentQueryExecutionTests()
    {
        // In-memory index — no disk, disposed per test class instance.
        var directoryFactory = new GenericDirectoryFactory(_ => new RAMDirectory());

        var options = Microsoft.Extensions.Options.Options.Create(new LuceneDirectoryIndexOptions
        {
            DirectoryFactory = directoryFactory,
        });

        _index = new LuceneIndex(
            NullLoggerFactory.Instance,
            "TestIndex",
            new TestOptionsMonitor(options.Value));

        // LuceneIndex.RunAsync defaults to true, so IndexItems queues the work onto a background
        // task and returns immediately — a query issued straight after observes an empty index and
        // returns no results. (WaitForChanges is no help here: it only reopens the NRT reader once
        // a generation has been written and the reopen thread exists, neither of which is true yet.)
        // IndexOperationComplete fires when the background batch has finished writing, after which
        // the first Searcher access reopens the reader to the committed generation. Blocking on it
        // makes every test deterministic — without it the assertions pass locally but flake in CI.
        using var indexingComplete = new ManualResetEventSlim(false);
        _index.IndexOperationComplete += OnIndexOperationComplete;

        // Index a handful of content items mirroring how Umbraco indexes nodeName / __IndexType.
        _index.IndexItems(
        [
            CreateItem("1", "Press Release"),
            CreateItem("2", "Pressure Vessel"),
            CreateItem("3", "Compress Archive"),
            CreateItem("4", "Homepage"),
        ]);

        if (!indexingComplete.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Examine indexing did not complete within 30 seconds.");
        }

        _index.IndexOperationComplete -= OnIndexOperationComplete;

        void OnIndexOperationComplete(object? sender, IndexOperationEventArgs e) => indexingComplete.Set();
    }

    [Fact]
    public void Contains_ExecutesWithoutThrowing_AndMatchesSubstring()
    {
        // Regression for #90: a leading-wildcard term must not throw and must match items
        // whose name *contains* the substring, including in the middle of the name.
        var query = FindContentAction.BuildQuery(
            "press", FindContentMatchMode.Contains, culture: null, contentTypeAliases: []);

        var results = Execute(query);

        var names = results.Select(r => r.Values["nodeName"]).ToList();
        names.ShouldContain("Press Release");
        names.ShouldContain("Pressure Vessel");
        names.ShouldContain("Compress Archive"); // substring in the middle — only a leading wildcard finds this
        names.ShouldNotContain("Homepage");
    }

    [Fact]
    public void Contains_LeadingWildcardThrows_WithoutTheFix()
    {
        // Documents the underlying failure the fix addresses: parsing the same native query
        // through Examine's default searcher (AllowLeadingWildcard = false) throws. This proves
        // the production fix (enabling AllowLeadingWildcard) is what keeps Contains working.
        var query = FindContentAction.BuildQuery(
            "press", FindContentMatchMode.Contains, culture: null, contentTypeAliases: []);

        Should.Throw<ParseException>(() => _index.Searcher
            .CreateQuery()
            .NativeQuery(query)
            .Execute(QueryOptions.SkipTake(0, 10)));
    }

    [Fact]
    public void StartsWith_ExecutesWithoutThrowing()
    {
        // Exact tokenisation/match semantics depend on Umbraco's real field-type config, which
        // this synthetic index does not replicate; the contract under test for #90 is that the
        // query parses and executes without throwing across every match mode.
        var query = FindContentAction.BuildQuery(
            "press", FindContentMatchMode.StartsWith, culture: null, contentTypeAliases: []);

        Should.NotThrow(() => Execute(query).ToList());
    }

    [Fact]
    public void Exact_ExecutesWithoutThrowing()
    {
        var query = FindContentAction.BuildQuery(
            "Homepage", FindContentMatchMode.Exact, culture: null, contentTypeAliases: []);

        Should.NotThrow(() => Execute(query).ToList());
    }

    /// <summary>
    /// Mirrors the production execution path: leading wildcards are enabled so the Contains
    /// query parses. Lives in the test rather than reflecting into the private production helper
    /// because the searcher type and options are exactly what production uses.
    /// </summary>
    private ISearchResults Execute(string query)
    {
        var searcher = (BaseLuceneSearcher)_index.Searcher;

        return searcher
            .CreateQuery(
                category: null,
                defaultOperation: BooleanOperation.And,
                luceneAnalyzer: searcher.LuceneAnalyzer,
                searchOptions: new Examine.Lucene.Search.LuceneSearchOptions { AllowLeadingWildcard = true })
            .NativeQuery(query)
            .Execute(QueryOptions.SkipTake(0, 10));
    }

    private static ValueSet CreateItem(string id, string nodeName)
        => ValueSet.FromObject(id, "content", new
        {
            nodeName,
            __IndexType = "content",
        });

    public void Dispose() => _index.Dispose();

    private sealed class TestOptionsMonitor(LuceneDirectoryIndexOptions options)
        : IOptionsMonitor<LuceneDirectoryIndexOptions>
    {
        public LuceneDirectoryIndexOptions CurrentValue => options;

        public LuceneDirectoryIndexOptions Get(string? name) => options;

        public IDisposable? OnChange(Action<LuceneDirectoryIndexOptions, string?> listener) => null;
    }
}
