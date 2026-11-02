using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class FindMediaActionTests
{
    private readonly Mock<ISearcherResolver> _searcherResolver = new();
    private readonly Mock<IEntityService> _entityService = new();
    private readonly Mock<IPublishedMediaCache> _publishedCache = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();
    private readonly FindMediaAction _action;

    public FindMediaActionTests()
    {
        var contextFactory = new Mock<IUmbracoContextFactory>();
        contextFactory
            .Setup(x => x.EnsureUmbracoContext())
            .Returns(new UmbracoContextReference(
                Mock.Of<IUmbracoContext>(),
                isRoot: false,
                Mock.Of<IUmbracoContextAccessor>()));

        _authorizer
            .Setup(a => a.FilterAuthorizedMediaAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> keys, CancellationToken _) => (IReadOnlySet<Guid>)keys.ToHashSet());

        _action = new FindMediaAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _searcherResolver.Object,
            _entityService.Object,
            _publishedCache.Object,
            contextFactory.Object,
            Mock.Of<IPublishedUrlProvider>(),
            _authorizer.Object,
            Mock.Of<ILogger<FindMediaAction>>());
    }

    [Fact]
    public async Task ExecuteAsync_NameOfOnlyWildcards_FailsValidation()
    {
        var result = await _action.ExecuteAsync(CreateContext(new FindMediaSettings { Name = "* ??" }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_NoSearcherForIndex_FailsAsUnknown()
    {
        _searcherResolver.Setup(r => r.GetSearcher(It.IsAny<string>())).Returns((ISearcher?)null);

        var result = await _action.ExecuteAsync(CreateContext(new FindMediaSettings { Name = "Logo" }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Unknown);
    }

    [Fact]
    public async Task ExecuteAsync_SearchesTheDraftMediaIndexWithNameAndTypeFilters()
    {
        var typeKey = Guid.NewGuid();
        var searcher = UseSearcher([]);

        await _action.ExecuteAsync(
            CreateContext(new FindMediaSettings { Name = "Company Logo", MediaTypes = typeKey.ToString() }), CancellationToken.None);

        var call = searcher.Calls.ShouldHaveSingleItem();
        call.IndexAlias.ShouldBe(UmbracoConstants.Search.IndexAliases.DraftMedia);
        call.Culture.ShouldBeNull();
        call.AccessContext.ShouldBeNull();
        call.Filters.OfType<TextFilter>().Select(f => f.Values.Single()).ShouldBe(["company", "logo"]);
        call.Filters.OfType<KeywordFilter>().Single(f => f.FieldName == UmbracoConstants.Search.FieldNames.ContentTypeId).Values.ShouldBe([typeKey.ToString("D")]);
    }

    [Fact]
    public async Task ExecuteAsync_NoHits_ReturnsNotFoundOutcome()
    {
        UseSearcher([]);

        var result = await _action.ExecuteAsync(CreateContext(new FindMediaSettings { Name = "Logo" }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(FindMediaAction.OutcomeNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_MapsMatchFromTheMediaEntity()
    {
        var key = Guid.NewGuid();
        UseSearcher([key]);
        _entityService
            .Setup(s => s.GetAll(UmbracoObjectTypes.Media, It.IsAny<Guid[]>()))
            .Returns([MediaEntity(key, "Company Logo", "image")]);

        var result = await _action.ExecuteAsync(CreateContext(new FindMediaSettings { Name = "logo", MatchMode = "Contains" }), CancellationToken.None);

        var match = ((FindMediaOutput)result.OutputData!).Matches.ShouldHaveSingleItem();
        match.MediaKey.ShouldBe(key);
        match.Name.ShouldBe("Company Logo");
        match.MediaTypeAlias.ShouldBe("image");
        match.Level.ShouldBe(2);
        match.Path.ShouldBe("-1,1200,1201");
        match.Url.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ExactMode_DropsHitsWhoseNameOnlyContainsTheWords()
    {
        var exact = Guid.NewGuid();
        var longer = Guid.NewGuid();
        UseSearcher([longer, exact]);
        _entityService
            .Setup(s => s.GetAll(UmbracoObjectTypes.Media, It.IsAny<Guid[]>()))
            .Returns([MediaEntity(longer, "Logo Dark", "image"), MediaEntity(exact, "Logo", "image")]);

        var result = await _action.ExecuteAsync(CreateContext(new FindMediaSettings { Name = "logo" }), CancellationToken.None);

        ((FindMediaOutput)result.OutputData!).Matches.Select(m => m.MediaKey).ShouldBe([exact]);
    }

    [Fact]
    public async Task ExecuteAsync_MatchesReachLimit_SetsLimitReached()
    {
        var keys = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        UseSearcher(keys);
        _entityService
            .Setup(s => s.GetAll(UmbracoObjectTypes.Media, It.IsAny<Guid[]>()))
            .Returns(keys.Select(k => MediaEntity(k, "Logo", "image")).ToList());

        var result = await _action.ExecuteAsync(CreateContext(new FindMediaSettings { Name = "logo", Limit = 2 }), CancellationToken.None);

        var output = (FindMediaOutput)result.OutputData!;
        output.Matches.Count.ShouldBe(2);
        output.LimitReached.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ServiceAccountCannotSeeAMatch_LeavesItOut()
    {
        var visible = Guid.NewGuid();
        var hidden = Guid.NewGuid();
        UseSearcher([visible, hidden]);
        _entityService
            .Setup(s => s.GetAll(UmbracoObjectTypes.Media, It.IsAny<Guid[]>()))
            .Returns([MediaEntity(visible, "Logo", "image"), MediaEntity(hidden, "Logo", "image")]);
        _authorizer
            .Setup(a => a.FilterAuthorizedMediaAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { visible });

        var result = await _action.ExecuteAsync(CreateContext(new FindMediaSettings { Name = "logo" }), CancellationToken.None);

        ((FindMediaOutput)result.OutputData!).Matches.Select(m => m.MediaKey).ShouldBe([visible]);
    }

    [Fact]
    public async Task ExecuteAsync_SendsTheTrashedMediaExclusionFilter()
    {
        var searcher = UseSearcher([]);

        await _action.ExecuteAsync(CreateContext(new FindMediaSettings { Name = "logo" }), CancellationToken.None);

        var filter = searcher.Calls.Single().Filters.OfType<KeywordFilter>()
            .Single(f => f.FieldName == UmbracoConstants.Search.FieldNames.PathIds);
        filter.Negate.ShouldBeTrue();
        filter.Values.ShouldBe([UmbracoConstants.System.RecycleBinMediaKey.ToString("D")]);
    }

    [Fact]
    public async Task ExecuteAsync_StartsWith_KeepsOnlyNamesWhereEveryTermStartsAWord()
    {
        var key = Guid.NewGuid();
        var other = Guid.NewGuid();
        UseSearcher([key, other]);
        _entityService
            .Setup(s => s.GetAll(UmbracoObjectTypes.Media, It.IsAny<Guid[]>()))
            .Returns([MediaEntity(key, "Company Logo", "image"), MediaEntity(other, "Banner", "image")]);

        var result = await _action.ExecuteAsync(
            CreateContext(new FindMediaSettings { Name = "logo", MatchMode = "StartsWith" }), CancellationToken.None);

        ((FindMediaOutput)result.OutputData!).Matches.Select(m => m.MediaKey).ShouldBe([key]);
    }

    [Fact]
    public async Task ExecuteAsync_Cancelled_StopsBeforeSearching()
    {
        var searcher = UseSearcher([Guid.NewGuid()]);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _action.ExecuteAsync(CreateContext(new FindMediaSettings { Name = "logo" }), cts.Token));

        searcher.Calls.ShouldBeEmpty();
    }

    private StubSearcher UseSearcher(IReadOnlyList<Guid> hits)
    {
        var searcher = new StubSearcher(hits, UmbracoObjectTypes.Media);
        _searcherResolver.Setup(r => r.GetSearcher(It.IsAny<string>())).Returns(searcher);
        return searcher;
    }

    private static IMediaEntitySlim MediaEntity(Guid key, string name, string alias)
        => Mock.Of<IMediaEntitySlim>(e =>
            e.Key == key && e.Name == name && e.ContentTypeAlias == alias && e.Level == 2 && e.Path == "-1,1200,1201");

    private static ActionContext CreateContext(FindMediaSettings settings)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.findMedia",
            Settings = settings,
        };
}
