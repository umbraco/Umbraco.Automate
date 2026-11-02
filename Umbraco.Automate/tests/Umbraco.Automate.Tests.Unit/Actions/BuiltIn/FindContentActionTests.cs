using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Search.Querying.Sorting;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class FindContentActionTests
{
    private readonly Mock<ISearcherResolver> _searcherResolver = new();
    private readonly Mock<IEntityService> _entityService = new();
    private readonly Mock<ILanguageService> _languageService = new();
    private readonly Mock<IPublishedContentCache> _publishedCache = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IPublishedUrlProvider> _urlProvider = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();
    private readonly FindContentAction _action;

    public FindContentActionTests()
    {
        _contextFactory
            .Setup(x => x.EnsureUmbracoContext())
            .Returns(new UmbracoContextReference(
                Mock.Of<IUmbracoContext>(),
                isRoot: false,
                Mock.Of<IUmbracoContextAccessor>()));

        _languageService
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync([Language("en-US", isDefault: true), Language("da-DK", isDefault: false)]);

        // Default: every match is authorised. Tests of the deny path override this.
        _authorizer
            .Setup(a => a.FilterAuthorizedContentAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> keys, IReadOnlySet<string> _, CancellationToken _) => (IReadOnlySet<Guid>)keys.ToHashSet());

        _urlProvider
            .Setup(p => p.GetUrl(It.IsAny<IPublishedContent>(), It.IsAny<UrlMode>(), It.IsAny<string?>(), It.IsAny<Uri?>()))
            .Returns("/found/");

        _action = new FindContentAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _searcherResolver.Object,
            _entityService.Object,
            _languageService.Object,
            _publishedCache.Object,
            _contextFactory.Object,
            _urlProvider.Object,
            _authorizer.Object,
            Mock.Of<ILogger<FindContentAction>>());
    }

    [Fact]
    public async Task ExecuteAsync_NameOfOnlyWildcards_FailsValidation()
    {
        var result = await _action.ExecuteAsync(CreateContext(new FindContentSettings { Name = "* ??" }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownMatchMode_FailsValidation()
    {
        var result = await _action.ExecuteAsync(
            CreateContext(new FindContentSettings { Name = "Home", MatchMode = "Fuzzy" }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_NoSearcherForIndex_FailsAsUnknown()
    {
        _searcherResolver.Setup(r => r.GetSearcher(It.IsAny<string>())).Returns((ISearcher?)null);

        var result = await _action.ExecuteAsync(CreateContext(new FindContentSettings { Name = "Home" }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Unknown);
    }

    [Fact]
    public async Task ExecuteAsync_Published_SearchesPublishedIndexWithNameFiltersInDefaultCulture()
    {
        var searcher = UseSearcher([]);

        await _action.ExecuteAsync(CreateContext(new FindContentSettings { Name = "Press Release" }), CancellationToken.None);

        var call = searcher.Calls.ShouldHaveSingleItem();
        call.IndexAlias.ShouldBe(UmbracoConstants.Search.IndexAliases.PublishedContent);
        call.Query.ShouldBeNull();
        call.Culture.ShouldBe("en-US");
        call.AccessContext.ShouldBeNull();
        call.Filters.OfType<TextFilter>().Select(f => f.Values.Single()).ShouldBe(["press", "release"]);
    }

    [Fact]
    public async Task ExecuteAsync_IncludeUnpublished_SearchesDraftIndex()
    {
        var searcher = UseSearcher([]);

        await _action.ExecuteAsync(
            CreateContext(new FindContentSettings { Name = "Home", IncludeUnpublished = true }), CancellationToken.None);

        searcher.Calls.ShouldHaveSingleItem().IndexAlias.ShouldBe(UmbracoConstants.Search.IndexAliases.DraftContent);
    }

    [Fact]
    public async Task ExecuteAsync_ContentTypes_FilterOnTheContentTypeKeys()
    {
        var typeKey = Guid.NewGuid();
        var searcher = UseSearcher([]);

        await _action.ExecuteAsync(
            CreateContext(new FindContentSettings { Name = "Home", ContentTypes = $"{typeKey},junk" }), CancellationToken.None);

        var typeFilter = searcher.Calls.Single().Filters.OfType<KeywordFilter>().Single(f => f.FieldName == UmbracoConstants.Search.FieldNames.ContentTypeId);
        typeFilter.FieldName.ShouldBe(UmbracoConstants.Search.FieldNames.ContentTypeId);
        typeFilter.Values.ShouldBe([typeKey.ToString("D")]);
    }

    [Fact]
    public async Task ExecuteAsync_CultureInDifferentCase_SearchesTheLanguagesOwnSpelling()
    {
        var searcher = UseSearcher([]);

        await _action.ExecuteAsync(
            CreateContext(new FindContentSettings { Name = "Home", Culture = " DA-dk " }), CancellationToken.None);

        searcher.Calls.Single().Culture.ShouldBe("da-DK");
    }

    [Fact]
    public async Task ExecuteAsync_NoHits_ReturnsNotFoundOutcome()
    {
        UseSearcher([]);

        var result = await _action.ExecuteAsync(CreateContext(new FindContentSettings { Name = "Home" }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(FindContentAction.OutcomeNotFound);
        ((FindContentOutput)result.OutputData!).Matches.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_Published_MapsMatchFromThePublishedCache()
    {
        var key = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        UseSearcher([key]);
        _publishedCache.Setup(c => c.GetById(key)).Returns(
            PublishedContent(key, "Homepage", "home", typeKey, level: 1, path: "-1,1100", cultures: []));

        var result = await _action.ExecuteAsync(CreateContext(new FindContentSettings { Name = "homepage" }), CancellationToken.None);
        result.Outcome.ShouldBeNull();
        result.Outcome.ShouldBeNull();
        var match = ((FindContentOutput)result.OutputData!).Matches.ShouldHaveSingleItem();
        match.ContentKey.ShouldBe(key);
        match.Name.ShouldBe("Homepage");
        match.ContentTypeAlias.ShouldBe("home");
        match.ContentTypeKey.ShouldBe(typeKey);
        match.Level.ShouldBe(1);
        match.Path.ShouldBe("-1,1100");
        match.Url.ShouldBe("/found/");
        match.Culture.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Published_UsesTheCultureNameWhenACultureIsRequested()
    {
        var key = Guid.NewGuid();
        UseSearcher([key]);
        _publishedCache.Setup(c => c.GetById(key)).Returns(
            PublishedContent(key, "Home", "home", Guid.NewGuid(), 1, "-1,1100", new Dictionary<string, string> { ["da-DK"] = "Forside" }));

        var result = await _action.ExecuteAsync(
            CreateContext(new FindContentSettings { Name = "forside", Culture = "da-dk" }), CancellationToken.None);

        var match = ((FindContentOutput)result.OutputData!).Matches.ShouldHaveSingleItem();
        match.Name.ShouldBe("Forside");
        match.Culture.ShouldBe("da-dk");
    }

    [Fact]
    public async Task ExecuteAsync_Draft_MapsMatchFromTheEntityAndResolvesUrlOnlyWhenPublished()
    {
        var key = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        UseSearcher([key]);
        _entityService
            .Setup(s => s.GetAll(UmbracoObjectTypes.Document, It.IsAny<Guid[]>()))
            .Returns([DocumentEntity(key, "Draft Page", "article", typeKey, new Dictionary<string, string>())]);

        var result = await _action.ExecuteAsync(
            CreateContext(new FindContentSettings { Name = "draft", IncludeUnpublished = true, MatchMode = "StartsWith" }),
            CancellationToken.None);

        var match = ((FindContentOutput)result.OutputData!).Matches.ShouldHaveSingleItem();
        match.ContentKey.ShouldBe(key);
        match.Name.ShouldBe("Draft Page");
        match.ContentTypeAlias.ShouldBe("article");
        match.ContentTypeKey.ShouldBe(typeKey);
        match.Url.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_StartsWith_KeepsOnlyNamesWhereEveryTermStartsAWord()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var other = Guid.NewGuid();
        UseSearcher([first, second, other]);
        _publishedCache.Setup(c => c.GetById(first)).Returns(PublishedContent(first, "The Homepage", "p", Guid.NewGuid(), 1, "-1", []));
        _publishedCache.Setup(c => c.GetById(second)).Returns(PublishedContent(second, "Home", "p", Guid.NewGuid(), 1, "-1", []));
        _publishedCache.Setup(c => c.GetById(other)).Returns(PublishedContent(other, "Archive", "p", Guid.NewGuid(), 1, "-1", []));

        var result = await _action.ExecuteAsync(
            CreateContext(new FindContentSettings { Name = "home", MatchMode = "StartsWith" }), CancellationToken.None);

        ((FindContentOutput)result.OutputData!).Matches.Select(m => m.ContentKey).ShouldBe([first, second]);
    }

    [Fact]
    public async Task ExecuteAsync_Exact_DropsHitsWhoseNameIsLongerAndKeepsPaging()
    {
        // 100 hits are fetched per page; the exact match sits on the second page.
        var others = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToArray();
        var wanted = Guid.NewGuid();
        var searcher = UseSearcher([.. others, wanted]);
        _publishedCache
            .Setup(c => c.GetById(It.Is<Guid>(k => k != wanted)))
            .Returns((Guid k) => PublishedContent(k, "News Archive 2", "n", Guid.NewGuid(), 1, "-1", []));
        _publishedCache.Setup(c => c.GetById(wanted)).Returns(PublishedContent(wanted, "News Archive", "n", Guid.NewGuid(), 1, "-1", []));

        var result = await _action.ExecuteAsync(
            CreateContext(new FindContentSettings { Name = "news archive", Limit = 2 }), CancellationToken.None);

        var output = (FindContentOutput)result.OutputData!;
        output.Matches.Select(m => m.ContentKey).ShouldBe([wanted]);
        output.LimitReached.ShouldBeFalse();
        searcher.Calls.Select(c => (c.Skip, c.Take)).ShouldBe([(0, 100), (100, 100)]);
    }

    [Fact]
    public async Task ExecuteAsync_Contains_NeedsTheValueAsWrittenInTheName()
    {
        var inOrder = Guid.NewGuid();
        var reversed = Guid.NewGuid();
        UseSearcher([reversed, inOrder]);
        _publishedCache.Setup(c => c.GetById(inOrder)).Returns(PublishedContent(inOrder, "The Press Release", "p", Guid.NewGuid(), 1, "-1", []));
        _publishedCache.Setup(c => c.GetById(reversed)).Returns(PublishedContent(reversed, "Release Press", "p", Guid.NewGuid(), 1, "-1", []));

        var result = await _action.ExecuteAsync(
            CreateContext(new FindContentSettings { Name = "press rel", MatchMode = "Contains" }), CancellationToken.None);

        ((FindContentOutput)result.OutputData!).Matches.Select(m => m.ContentKey).ShouldBe([inOrder]);
    }

    [Fact]
    public async Task ExecuteAsync_SendsTheTrashedContentExclusionFilter()
    {
        var searcher = UseSearcher([]);

        await _action.ExecuteAsync(CreateContext(new FindContentSettings { Name = "home", IncludeUnpublished = true }), CancellationToken.None);

        var filter = searcher.Calls.Single().Filters.OfType<KeywordFilter>()
            .Single(f => f.FieldName == UmbracoConstants.Search.FieldNames.PathIds);
        filter.Negate.ShouldBeTrue();
        filter.Values.ShouldBe([UmbracoConstants.System.RecycleBinContentKey.ToString("D")]);
    }

    [Fact]
    public async Task ExecuteAsync_HitCeilingReachedWithNoMatches_StopsPaging()
    {
        var hits = Enumerable.Range(0, FindByNameSearch.MaxHitsExamined + 500).Select(_ => Guid.NewGuid()).ToArray();
        var searcher = UseSearcher(hits);
        var other = PublishedContent(Guid.NewGuid(), "Something else", "n", Guid.NewGuid(), 1, "-1", []);
        _publishedCache.Setup(c => c.GetById(It.IsAny<Guid>())).Returns(other);

        var result = await _action.ExecuteAsync(
            CreateContext(new FindContentSettings { Name = "wanted", Limit = 5 }), CancellationToken.None);

        result.Outcome.ShouldBe(FindContentAction.OutcomeNotFound);
        searcher.Calls.Count.ShouldBe(FindByNameSearch.MaxHitsExamined / FindByNameSearch.MinPageSize);
        searcher.Calls[^1].Skip.ShouldBe(FindByNameSearch.MaxHitsExamined - FindByNameSearch.MinPageSize);
    }

    [Fact]
    public async Task ExecuteAsync_HitCeilingReachedWithSomeMatches_SetsLimitReached()
    {
        var wanted = Guid.NewGuid();
        var hits = new[] { wanted }.Concat(Enumerable.Range(0, FindByNameSearch.MaxHitsExamined + 500).Select(_ => Guid.NewGuid())).ToArray();
        UseSearcher(hits);
        var other = PublishedContent(Guid.NewGuid(), "Something else", "n", Guid.NewGuid(), 1, "-1", []);
        _publishedCache.Setup(c => c.GetById(It.IsAny<Guid>())).Returns(other);
        _publishedCache.Setup(c => c.GetById(wanted)).Returns(PublishedContent(wanted, "Wanted", "n", Guid.NewGuid(), 1, "-1", []));

        var result = await _action.ExecuteAsync(
            CreateContext(new FindContentSettings { Name = "wanted", Limit = 5 }), CancellationToken.None);

        var output = (FindContentOutput)result.OutputData!;
        output.Matches.Select(m => m.ContentKey).ShouldBe([wanted]);
        output.LimitReached.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_SameKeyOnTwoPages_IsLoadedAndReturnedOnce()
    {
        var duplicated = Guid.NewGuid();
        var filler = Enumerable.Range(0, 99).Select(_ => Guid.NewGuid()).ToArray();
        UseSearcher([duplicated, .. filler, duplicated]);
        _publishedCache
            .Setup(c => c.GetById(It.IsAny<Guid>()))
            .Returns((Guid k) => PublishedContent(k, "Page", "n", Guid.NewGuid(), 1, "-1", []));

        var result = await _action.ExecuteAsync(
            CreateContext(new FindContentSettings { Name = "page", Limit = 500 }), CancellationToken.None);

        var keys = ((FindContentOutput)result.OutputData!).Matches.Select(m => m.ContentKey).ToList();
        keys.Count.ShouldBe(100);
        keys.Count(k => k == duplicated).ShouldBe(1);
        _publishedCache.Verify(c => c.GetById(duplicated), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Draft_UsesTheCultureNameForTheRequestedCulture()
    {
        var key = Guid.NewGuid();
        UseSearcher([key]);
        _entityService
            .Setup(s => s.GetAll(UmbracoObjectTypes.Document, It.IsAny<Guid[]>()))
            .Returns([DocumentEntity(key, "Home", "home", Guid.NewGuid(), new Dictionary<string, string> { ["da-DK"] = "Forside" })]);

        var result = await _action.ExecuteAsync(
            CreateContext(new FindContentSettings { Name = "forside", Culture = "da-dk", IncludeUnpublished = true }),
            CancellationToken.None);

        var match = ((FindContentOutput)result.OutputData!).Matches.ShouldHaveSingleItem();
        match.Name.ShouldBe("Forside");
        match.Culture.ShouldBe("da-dk");
    }

    [Fact]
    public async Task ExecuteAsync_SearchThrows_FailsAsUnknown()
    {
        var searcher = new Mock<ISearcher>();
        searcher
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<IEnumerable<Filter>?>(), It.IsAny<IEnumerable<Facet>?>(),
                It.IsAny<IEnumerable<Sorter>?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AccessContext?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("index unavailable"));
        _searcherResolver.Setup(r => r.GetSearcher(It.IsAny<string>())).Returns(searcher.Object);

        var result = await _action.ExecuteAsync(CreateContext(new FindContentSettings { Name = "home" }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Unknown);
    }

    [Fact]
    public async Task ExecuteAsync_Cancelled_StopsBeforeSearching()
    {
        var searcher = UseSearcher([Guid.NewGuid()]);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _action.ExecuteAsync(CreateContext(new FindContentSettings { Name = "home" }), cts.Token));

        searcher.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_MatchesReachLimit_SetsLimitReached()
    {
        var keys = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        UseSearcher(keys);
        foreach (var key in keys)
        {
            _publishedCache.Setup(c => c.GetById(key)).Returns(PublishedContent(key, "Page", "p", Guid.NewGuid(), 1, "-1", []));
        }

        var result = await _action.ExecuteAsync(
            CreateContext(new FindContentSettings { Name = "page", Limit = 2 }), CancellationToken.None);

        var output = (FindContentOutput)result.OutputData!;
        output.Matches.Count.ShouldBe(2);
        output.LimitReached.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ServiceAccountCannotSeeAMatch_LeavesItOut()
    {
        var visible = Guid.NewGuid();
        var hidden = Guid.NewGuid();
        UseSearcher([visible, hidden]);
        foreach (var key in new[] { visible, hidden })
        {
            _publishedCache.Setup(c => c.GetById(key)).Returns(PublishedContent(key, "Page", "p", Guid.NewGuid(), 1, "-1", []));
        }

        _authorizer
            .Setup(a => a.FilterAuthorizedContentAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { visible });

        var result = await _action.ExecuteAsync(CreateContext(new FindContentSettings { Name = "page" }), CancellationToken.None);

        ((FindContentOutput)result.OutputData!).Matches.Select(m => m.ContentKey).ShouldBe([visible]);
    }

    private StubSearcher UseSearcher(IReadOnlyList<Guid> hits)
    {
        var searcher = new StubSearcher(hits, UmbracoObjectTypes.Document);
        _searcherResolver.Setup(r => r.GetSearcher(It.IsAny<string>())).Returns(searcher);
        return searcher;
    }

    private static ILanguage Language(string isoCode, bool isDefault)
        => Mock.Of<ILanguage>(l => l.IsoCode == isoCode && l.IsDefault == isDefault);

    private static IPublishedContent PublishedContent(
        Guid key, string name, string alias, Guid typeKey, int level, string path, Dictionary<string, string> cultures)
    {
        var type = Mock.Of<IPublishedContentType>(t => t.Alias == alias && t.Key == typeKey && t.ItemType == PublishedItemType.Content);
        var cultureInfos = cultures.ToDictionary(
            c => c.Key,
            c => new PublishedCultureInfo(c.Key, c.Value, null, DateTime.UtcNow));

        return Mock.Of<IPublishedContent>(c =>
            c.Key == key
            && c.Name == name
            && c.ContentType == type
            && c.Level == level
            && c.Path == path
            && c.Cultures == (IReadOnlyDictionary<string, PublishedCultureInfo>)cultureInfos
            && c.CreateDate == new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)
            && c.UpdateDate == new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc));
    }

    private static IDocumentEntitySlim DocumentEntity(
        Guid key, string name, string alias, Guid typeKey, Dictionary<string, string> cultureNames)
        => Mock.Of<IDocumentEntitySlim>(e =>
            e.Key == key
            && e.Name == name
            && e.ContentTypeAlias == alias
            && e.ContentTypeKey == typeKey
            && e.Level == 2
            && e.Path == "-1,1100,1101"
            && e.CultureNames == (IReadOnlyDictionary<string, string>)cultureNames);

    private static ActionContext CreateContext(FindContentSettings settings)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.findContent",
            Settings = settings,
        };
}
