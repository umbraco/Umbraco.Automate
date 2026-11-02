using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Automate.Tests.Integration.Search;

/// <summary>
/// Runs Find Content / Find Media's name search against the real Umbraco Search Examine provider
/// (its indexer, searcher, field definitions and analyzers) rather than a stubbed searcher, so the
/// filters the actions build are proven to find what they claim to.
/// </summary>
public sealed class FindByNameUmbracoSearchTests : IClassFixture<FindByNameUmbracoSearchTests.Fixture>
{
    private readonly Fixture _fixture;

    public FindByNameUmbracoSearchTests(Fixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Exact_MultiWordName_FindsOnlyTheWholeName()
    {
        (await FindContentAsync("press release", FindContentMatchMode.Exact)).ShouldBe(["Press Release"]);
        (await FindContentAsync("press", FindContentMatchMode.Exact)).ShouldBeEmpty();
    }

    [Fact]
    public async Task StartsWith_MultiWordName_NeedsEveryWordToStartAWord()
        => (await FindContentAsync("press rel", FindContentMatchMode.StartsWith)).ShouldBe(["Press Release"]);

    [Fact]
    public async Task StartsWith_FindsAnyWordThatStartsWithTheText()
    {
        (await FindContentAsync("home", FindContentMatchMode.StartsWith)).ShouldBe(["The Homepage"]);
        (await FindContentAsync("press", FindContentMatchMode.StartsWith)).ShouldBe(["Press Release", "Pressure Vessel"]);
    }

    [Fact]
    public async Task Contains_FindsWordsAndWordStartsButNotTextInsideAWord()
    {
        (await FindContentAsync("press", FindContentMatchMode.Contains)).ShouldBe(["Press Release", "Pressure Vessel"]);
        (await FindContentAsync("press release", FindContentMatchMode.Contains)).ShouldBe(["Press Release"]);
    }

    [Theory]
    [InlineData("children's books", FindContentMatchMode.Exact, "Children's Books")]
    [InlineData("children's", FindContentMatchMode.StartsWith, "Children's Books")]
    [InlineData("child", FindContentMatchMode.StartsWith, "Children's Books")]
    [InlineData("o'brien", FindContentMatchMode.Exact, "O'Brien")]
    [InlineData("o'br", FindContentMatchMode.StartsWith, "O'Brien")]
    [InlineData("release 2.0", FindContentMatchMode.Exact, "Release 2.0")]
    [InlineData("2.0", FindContentMatchMode.Contains, "Release 2.0")]
    [InlineData("c++ guide", FindContentMatchMode.Exact, "C++ Guide")]
    [InlineData("q&a (faq)", FindContentMatchMode.Exact, "Q&A (FAQ)")]
    public async Task NamesWithPunctuationInsideAWord_AreFound(string name, FindContentMatchMode mode, string expected)
        => (await FindContentAsync(name, mode)).ShouldBe([expected]);

    [Fact]
    public async Task UpperCaseInput_StillMatchesWordStarts()
        => (await FindContentAsync("PRESS REL", FindContentMatchMode.StartsWith)).ShouldBe(["Press Release"]);

    [Theory]
    [InlineData("the homepage", FindContentMatchMode.Exact, "The Homepage")]
    [InlineData("the home", FindContentMatchMode.StartsWith, "The Homepage")]
    [InlineData("terms and conditions", FindContentMatchMode.Exact, "Terms and Conditions")]
    [InlineData("terms and conditions", FindContentMatchMode.Contains, "Terms and Conditions")]
    [InlineData("and cond", FindContentMatchMode.StartsWith, "Terms and Conditions")]
    [InlineData("news & events", FindContentMatchMode.Exact, "News & Events")]
    [InlineData("news & ev", FindContentMatchMode.StartsWith, "News & Events")]
    [InlineData("press  release", FindContentMatchMode.Exact, "Press Release")]
    [InlineData("press  release", FindContentMatchMode.Contains, "Press Release")]
    public async Task NamesWithStopWordsAndSymbols_AreFound(string name, FindContentMatchMode mode, string expected)
        => (await FindContentAsync(name, mode)).ShouldBe([expected]);

    [Theory]
    [InlineData("hallo-xyz")]
    [InlineData("c++")]
    [InlineData("q&a")]
    [InlineData("c#")]
    public async Task StartsWith_DoesNotMatchNamesThatOnlyShareASplitPieceOfThePunctuatedTerm(string name)
    {
        var found = await FindContentAsync(name, FindContentMatchMode.StartsWith);

        found.ShouldNotContain("Hallo-World Page");
        found.ShouldNotContain("Plan C");
        found.ShouldNotContain("Q Branch");
    }

    [Theory]
    [InlineData("hallo-world", "Hallo-World Page")]
    [InlineData("c++", "C++ Guide")]
    [InlineData("q&a", "Q&A (FAQ)")]
    public async Task StartsWith_FindsTheNameWhenThePunctuatedTermMatches(string name, string expected)
        => (await FindContentAsync(name, FindContentMatchMode.StartsWith)).ShouldBe([expected]);

    [Theory]
    [InlineData("world", "Hallo-World Page")]
    [InlineData("faq", "Q&A (FAQ)")]
    [InlineData("events", "News & Events", "News&Events")]
    [InlineData("draft", "[Draft] Landing")]
    public async Task StartsWith_FindsWordsThatStartAfterPunctuation(string name, params string[] expected)
        => (await FindContentAsync(name, FindContentMatchMode.StartsWith)).ShouldBe(expected.Order());

    [Fact]
    public async Task NamesThatReallyStartWithWildcardCharacters_AreFoundByExactAndContains()
    {
        (await FindContentAsync("*New* Products", FindContentMatchMode.Exact)).ShouldBe(["*New* Products"]);
        (await FindContentAsync("*new* prod", FindContentMatchMode.Contains)).ShouldBe(["*New* Products"]);
    }

    [Fact]
    public async Task TheIndex_HoldsNoTokenForAStopWord()
    {
        // Proves the analyzer really drops stop words, which is why they are never sent as filters.
        var raw = await _fixture.Host.Searcher.SearchAsync(
            UmbracoConstants.Search.IndexAliases.PublishedContent,
            filters: [new TextFilter(UmbracoConstants.Search.FieldNames.Name, ["the"], false)]);

        raw.Total.ShouldBe(0);
    }

    [Fact]
    public async Task NameOfOnlyStopWordsAndSymbols_ScansWithTheNameCheckDoingTheWork()
    {
        // No term can be sent to the index, so the search is the type and trash constraints alone.
        (await FindContentAsync("the", FindContentMatchMode.StartsWith)).ShouldBe(["The Homepage"]);
        (await FindContentAsync("&", FindContentMatchMode.Contains)).ShouldBe(["News & Events", "News&Events", "Q&A (FAQ)"]);
        (await FindContentAsync("and", FindContentMatchMode.Exact)).ShouldBeEmpty();
    }

    [Fact]
    public void IndexStopWords_MatchTheAnalyzersDefaultStopSet()
        => FindByNameSearch.IndexStopWords.Order()
            .ShouldBe(Lucene.Net.Analysis.Core.StopAnalyzer.ENGLISH_STOP_WORDS_SET.Select(w => w.ToString()!).Order());

    [Fact]
    public async Task LeadingWildcardCharacters_AreIgnored()
        => (await FindContentAsync("*press ?rel", FindContentMatchMode.StartsWith)).ShouldBe(["Press Release"]);

    [Fact]
    public async Task Culture_FindsThatCulturesNameAndInvariantItems()
    {
        (await FindContentAsync("hej", FindContentMatchMode.StartsWith, culture: "da-DK")).ShouldBe(["Hej Verden"]);
        (await FindContentAsync("hello", FindContentMatchMode.StartsWith, culture: "da-DK")).ShouldBeEmpty();
        (await FindContentAsync("hello", FindContentMatchMode.StartsWith, culture: "en-US")).ShouldBe(["Hello World"]);

        // Invariant items are found whichever culture is searched.
        (await FindContentAsync("homepage", FindContentMatchMode.StartsWith, culture: "da-DK")).ShouldBe(["The Homepage"]);
    }

    [Fact]
    public async Task Culture_NoneGiven_OnlyFindsInvariantItems()
    {
        (await FindContentAsync("hello", FindContentMatchMode.StartsWith)).ShouldBeEmpty();
        (await FindContentAsync("homepage", FindContentMatchMode.StartsWith)).ShouldBe(["The Homepage"]);
    }

    [Fact]
    public async Task ContentTypeFilter_RestrictsToTheGivenTypes()
    {
        (await FindContentAsync("archive", FindContentMatchMode.StartsWith)).ShouldBe(["Compress Archive"]);
        (await FindContentAsync("archive", FindContentMatchMode.StartsWith, typeKeys: [_fixture.TypeA])).ShouldBeEmpty();
        (await FindContentAsync("archive", FindContentMatchMode.StartsWith, typeKeys: [_fixture.TypeA, _fixture.TypeB]))
            .ShouldBe(["Compress Archive"]);
    }

    [Fact]
    public async Task TrashedContent_IsLeftOut_OfTheDraftIndex()
    {
        // Control: without the recycle bin filter the trashed item is in the index and is found.
        var unfiltered = await _fixture.Host.Searcher.SearchAsync(
            UmbracoConstants.Search.IndexAliases.DraftContent,
            filters: [new TextFilter(UmbracoConstants.Search.FieldNames.Name, ["trashed"], false)]);
        unfiltered.Total.ShouldBe(1);

        (await FindContentAsync("trashed", FindContentMatchMode.StartsWith, alias: UmbracoConstants.Search.IndexAliases.DraftContent))
            .ShouldBeEmpty();
        (await FindContentAsync("draft", FindContentMatchMode.StartsWith, alias: UmbracoConstants.Search.IndexAliases.DraftContent))
            .ShouldBe(["Draft Only"]);
    }

    [Fact]
    public async Task TrashedMedia_IsLeftOut_OfTheMediaIndex()
    {
        (await FindMediaAsync("logo", FindContentMatchMode.StartsWith)).ShouldBe(["Company Logo"]);
        (await FindMediaAsync("deleted", FindContentMatchMode.StartsWith)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ProtectedContent_IsLeftOut_OfThePublishedIndex()
    {
        // Control: bypassing protection shows the protected item is indexed.
        var bypassed = await _fixture.Host.Searcher.SearchAsync(
            UmbracoConstants.Search.IndexAliases.PublishedContent,
            filters: [new TextFilter(UmbracoConstants.Search.FieldNames.Name, ["protected"], false)],
            accessContext: AccessContext.BypassProtection());
        bypassed.Total.ShouldBe(1);

        (await FindContentAsync("protected", FindContentMatchMode.StartsWith)).ShouldBeEmpty();
    }

    [Fact]
    public async Task LimitAndPaging_ReturnTheFirstMatchesAndSayMoreMayExist()
    {
        var result = await SearchAsync(
            UmbracoConstants.Search.IndexAliases.PublishedContent,
            "press",
            FindContentMatchMode.StartsWith,
            culture: null,
            typeKeys: [],
            UmbracoConstants.System.RecycleBinContentKey,
            limit: 1);

        result.Matches.Count.ShouldBe(1);
        result.LimitReached.ShouldBeTrue();
    }

    private async Task<string[]> FindContentAsync(
        string name,
        FindContentMatchMode mode,
        string? culture = null,
        Guid[]? typeKeys = null,
        string alias = UmbracoConstants.Search.IndexAliases.PublishedContent)
        => Names(await SearchAsync(alias, name, mode, culture, typeKeys ?? [], UmbracoConstants.System.RecycleBinContentKey, limit: 50));

    private async Task<string[]> FindMediaAsync(string name, FindContentMatchMode mode)
        => Names(await SearchAsync(
            UmbracoConstants.Search.IndexAliases.DraftMedia,
            name,
            mode,
            culture: null,
            typeKeys: [],
            UmbracoConstants.System.RecycleBinMediaKey,
            limit: 50));

    private Task<FindByNameSearchResult<(Guid Key, string? Name)>> SearchAsync(
        string alias,
        string name,
        FindContentMatchMode mode,
        string? culture,
        IReadOnlyCollection<Guid> typeKeys,
        Guid recycleBinKey,
        int limit)
        => FindByNameSearch.SearchAsync(
            _fixture.Host.Searcher,
            alias,
            name,
            mode,
            culture,
            FindByNameSearch.BuildFilters(name, typeKeys, recycleBinKey),
            limit,
            keys => keys
                .Select(key => (Key: key, Name: _fixture.NameOf(key, culture)))
                .Where(item => item.Name is not null)
                .ToList(),
            item => item.Name,
            CancellationToken.None);

    private static string[] Names(FindByNameSearchResult<(Guid Key, string? Name)> result)
        => result.Matches.Select(m => m.Name!).Order().ToArray();

    public sealed class Fixture : IAsyncLifetime
    {
        private readonly Dictionary<Guid, Dictionary<string, string>> _namesByKey = [];

        public UmbracoSearchTestHost Host { get; } = new();

        public Guid TypeA { get; } = Guid.NewGuid();

        public Guid TypeB { get; } = Guid.NewGuid();

        /// <summary>Reads an item's name in a culture, falling back to its invariant name.</summary>
        public string? NameOf(Guid key, string? culture)
        {
            var names = _namesByKey[key];
            return culture is not null && names.TryGetValue(culture, out var cultureName)
                ? cultureName
                : names.GetValueOrDefault(string.Empty);
        }

        public async Task InitializeAsync()
        {
            const string published = UmbracoConstants.Search.IndexAliases.PublishedContent;
            const string draft = UmbracoConstants.Search.IndexAliases.DraftContent;
            const string media = UmbracoConstants.Search.IndexAliases.DraftMedia;
            var root = Guid.NewGuid();

            foreach (var name in new[]
            {
                "Press Release", "Pressure Vessel", "The Homepage", "Children's Books", "O'Brien",
                "Release 2.0", "C++ Guide", "Q&A (FAQ)", "Hallo-World Page", "Plan C", "Q Branch",
                "Terms and Conditions", "News & Events", "News&Events", "[Draft] Landing", "*New* Products",
            })
            {
                await AddInvariantAsync(published, UmbracoObjectTypes.Document, name, TypeA, [root]);
            }

            await AddInvariantAsync(published, UmbracoObjectTypes.Document, "Compress Archive", TypeB, [root]);
            await AddInvariantAsync(
                published,
                UmbracoObjectTypes.Document,
                "Press Protected",
                TypeA,
                [root],
                protectedFor: [Guid.NewGuid()]);

            // A variant item has one document per culture and no invariant one.
            await AddAsync(
                published,
                UmbracoObjectTypes.Document,
                Guid.NewGuid(),
                TypeA,
                [("en-US", "Hello World"), ("da-DK", "Hej Verden")],
                [root]);

            await AddInvariantAsync(draft, UmbracoObjectTypes.Document, "Draft Only", TypeA, [root]);
            await AddInvariantAsync(
                draft,
                UmbracoObjectTypes.Document,
                "Press Trashed",
                TypeA,
                [UmbracoConstants.System.RecycleBinContentKey, root]);

            await AddInvariantAsync(media, UmbracoObjectTypes.Media, "Company Logo", TypeA, [root]);
            await AddInvariantAsync(
                media,
                UmbracoObjectTypes.Media,
                "Deleted Logo",
                TypeA,
                [UmbracoConstants.System.RecycleBinMediaKey, root]);

            await Host.WaitUntilSettledAsync();
        }

        public Task DisposeAsync()
        {
            Host.Dispose();
            return Task.CompletedTask;
        }

        private Task AddInvariantAsync(
            string alias,
            UmbracoObjectTypes objectType,
            string name,
            Guid typeKey,
            Guid[] ancestors,
            Guid[]? protectedFor = null)
            => AddAsync(alias, objectType, Guid.NewGuid(), typeKey, [(null, name)], ancestors, protectedFor);

        private Task AddAsync(
            string alias,
            UmbracoObjectTypes objectType,
            Guid key,
            Guid typeKey,
            IReadOnlyList<(string? Culture, string Name)> names,
            Guid[] ancestors,
            Guid[]? protectedFor = null)
        {
            // Names are kept per culture, with the invariant name under the empty string.
            _namesByKey[key] = names.ToDictionary(item => item.Culture ?? string.Empty, item => item.Name);

            return Host.IndexAsync(alias, objectType, key, typeKey, names, ancestors, protectedFor);
        }
    }
}
