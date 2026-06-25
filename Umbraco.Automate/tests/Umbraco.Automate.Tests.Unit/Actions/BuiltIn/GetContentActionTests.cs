using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class GetContentActionTests
{
    private readonly Mock<IPublishedContentCache> _cache = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IPublishedUrlProvider> _urlProvider = new();
    private readonly Mock<IUserIdKeyResolver> _userIdKeyResolver = new();
    private readonly Mock<IContentValueNormaliser> _normaliser = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();
    private readonly Mock<IDocumentNavigationQueryService> _navigationQueryService = new();
    private readonly GetContentAction _action;

    public GetContentActionTests()
    {
        _contextFactory
            .Setup(x => x.EnsureUmbracoContext())
            .Returns(new UmbracoContextReference(
                Mock.Of<IUmbracoContext>(),
                isRoot: false,
                Mock.Of<IUmbracoContextAccessor>()));

        // Default: any user id resolves to a failed attempt (treated as "deleted user").
        // Tests that care about a specific resolution override per-user-id.
        _userIdKeyResolver
            .Setup(x => x.TryGetAsync(It.IsAny<int>()))
            .ReturnsAsync(Attempt<Guid>.Fail());

        // Default: node-level authorisation passes. Tests that exercise the deny path
        // override this on a per-test basis.
        _authorizer
            .Setup(a => a.AuthorizeContentAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _action = new GetContentAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _cache.Object,
            _contextFactory.Object,
            _urlProvider.Object,
            _userIdKeyResolver.Object,
            _normaliser.Object,
            _authorizer.Object,
            _navigationQueryService.Object,
            Mock.Of<ILogger<GetContentAction>>());
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.getContent");

    [Fact]
    public void HasCorrectName()
        => _action.Name.ShouldBe("Get Content");

    [Fact]
    public async Task ExecuteAsync_InvalidContentKey_ReturnsValidationError()
    {
        var context = CreateContext(new GetContentSettings { ContentKey = "not-a-guid" });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyContentKey_ReturnsValidationError()
    {
        var context = CreateContext(new GetContentSettings { ContentKey = "" });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_NodeAuthorizationDenied_ReturnsAuthenticationError()
    {
        var contentKey = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeContentAsync(contentKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Out of start-node path."));

        var context = CreateContext(new GetContentSettings { ContentKey = contentKey.ToString() });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
        _cache.Verify(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool?>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ContentNotFound_ReturnsNotFoundOutcome()
    {
        var contentKey = Guid.NewGuid();
        _cache.Setup(x => x.GetByIdAsync(contentKey, It.IsAny<bool?>()))
            .ReturnsAsync((IPublishedContent?)null);

        var context = CreateContext(new GetContentSettings { ContentKey = contentKey.ToString() });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(GetContentAction.OutcomeNotFound);

        var output = result.OutputData as GetContentOutput;
        output.ShouldNotBeNull();
        output.ContentKey.ShouldBe(contentKey);
    }

    [Fact]
    public async Task ExecuteAsync_ContentFound_ReturnsProjectedOutput()
    {
        var contentKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        var creatorKey = Guid.NewGuid();
        var writerKey = Guid.NewGuid();
        var content = MockPublishedContent(contentKey, "About", "page", contentTypeKey, creatorId: 5, writerId: 7);

        _cache.Setup(x => x.GetByIdAsync(contentKey, It.IsAny<bool?>()))
            .ReturnsAsync(content.Object);

        _userIdKeyResolver.Setup(x => x.TryGetAsync(5)).ReturnsAsync(Attempt<Guid>.Succeed(creatorKey));
        _userIdKeyResolver.Setup(x => x.TryGetAsync(7)).ReturnsAsync(Attempt<Guid>.Succeed(writerKey));

        var properties = new Dictionary<string, object?> { ["title"] = "Hello" };
        _normaliser.Setup(x => x.NormaliseProperties(content.Object, null))
            .Returns(properties);

        var context = CreateContext(new GetContentSettings { ContentKey = contentKey.ToString() });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();

        var output = result.OutputData as GetContentOutput;
        output.ShouldNotBeNull();
        output.ContentKey.ShouldBe(contentKey);
        output.Name.ShouldBe("About");
        output.ContentTypeAlias.ShouldBe("page");
        output.ContentTypeKey.ShouldBe(contentTypeKey);
        output.CreatorKey.ShouldBe(creatorKey);
        output.WriterKey.ShouldBe(writerKey);
        output.Properties.ShouldContainKey("title");
        output.Properties["title"].ShouldBe("Hello");
    }

    [Fact]
    public async Task ExecuteAsync_DeletedUser_ReturnsNullCreatorKey()
    {
        var contentKey = Guid.NewGuid();
        // Default resolver setup returns failed Attempt for any int — simulating a deleted user.
        var content = MockPublishedContent(contentKey, "About", "page", Guid.NewGuid(), creatorId: 99, writerId: 99);

        _cache.Setup(x => x.GetByIdAsync(contentKey, It.IsAny<bool?>()))
            .ReturnsAsync(content.Object);

        var context = CreateContext(new GetContentSettings { ContentKey = contentKey.ToString() });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        var output = result.OutputData as GetContentOutput;
        output!.CreatorKey.ShouldBeNull();
        output.WriterKey.ShouldBeNull();
    }

    private static Mock<IPublishedContent> MockPublishedContent(
        Guid key,
        string name,
        string typeAlias,
        Guid contentTypeKey,
        int creatorId = 0,
        int writerId = 0)
    {
        var contentType = new Mock<IPublishedContentType>();
        contentType.SetupGet(x => x.Alias).Returns(typeAlias);
        contentType.SetupGet(x => x.Key).Returns(contentTypeKey);
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        contentType.SetupGet(x => x.ItemType).Returns(PublishedItemType.Content);

        var content = new Mock<IPublishedContent>();
        content.SetupGet(x => x.Key).Returns(key);
        content.SetupGet(x => x.Name).Returns(name);
        content.SetupGet(x => x.ContentType).Returns(contentType.Object);
        content.SetupGet(x => x.Cultures).Returns(new Dictionary<string, PublishedCultureInfo>(StringComparer.OrdinalIgnoreCase));
        content.SetupGet(x => x.Properties).Returns(Array.Empty<IPublishedProperty>());
        content.SetupGet(x => x.CreateDate).Returns(DateTime.UtcNow);
        content.SetupGet(x => x.UpdateDate).Returns(DateTime.UtcNow);
        content.SetupGet(x => x.CreatorId).Returns(creatorId);
        content.SetupGet(x => x.WriterId).Returns(writerId);
        return content;
    }

    private static ActionContext CreateContext(GetContentSettings settings)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.getContent",
            Settings = settings,
        };
}
