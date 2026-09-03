using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class GetMediaActionTests
{
    private readonly Mock<IPublishedMediaCache> _cache = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IPublishedUrlProvider> _urlProvider = new();
    private readonly Mock<IUserIdKeyResolver> _userIdKeyResolver = new();
    private readonly Mock<IContentValueNormaliser> _normaliser = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();

    // Real accessor, not a mock: IsAvailable == false makes it fall back to its AsyncLocal,
    // which is exactly what the accessor does on a background thread with no HTTP request.
    private readonly IVariationContextAccessor _variationContextAccessor =
        new HybridVariationContextAccessor(Mock.Of<IRequestCache>(c => c.IsAvailable == false));

    private readonly GetMediaAction _action;

    public GetMediaActionTests()
    {
        // The AsyncLocal behind the accessor is static, so clear it rather than inherit
        // a value from an unrelated test.
        _variationContextAccessor.VariationContext = null;

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
            .Setup(a => a.AuthorizeMediaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _action = new GetMediaAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _cache.Object,
            _contextFactory.Object,
            _urlProvider.Object,
            _userIdKeyResolver.Object,
            _normaliser.Object,
            _authorizer.Object,
            _variationContextAccessor,
            Mock.Of<ILogger<GetMediaAction>>());
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.getMedia");

    [Fact]
    public void HasCorrectName()
        => _action.Name.ShouldBe("Get Media");

    [Fact]
    public async Task ExecuteAsync_InvalidMediaKey_ReturnsValidationError()
    {
        var context = CreateContext(new GetMediaSettings { MediaKey = "not-a-guid" });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyMediaKey_ReturnsValidationError()
    {
        var context = CreateContext(new GetMediaSettings { MediaKey = "" });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_NodeAuthorizationDenied_ReturnsAuthenticationError()
    {
        var mediaKey = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeMediaAsync(mediaKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Out of start-node path."));

        var context = CreateContext(new GetMediaSettings { MediaKey = mediaKey.ToString() });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
        _cache.Verify(c => c.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_MediaNotFound_ReturnsNotFoundOutcome()
    {
        var mediaKey = Guid.NewGuid();
        _cache.Setup(x => x.GetByIdAsync(mediaKey))
            .ReturnsAsync((IPublishedContent?)null);

        var context = CreateContext(new GetMediaSettings { MediaKey = mediaKey.ToString() });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(GetMediaAction.OutcomeNotFound);

        var output = result.OutputData as GetMediaOutput;
        output.ShouldNotBeNull();
        output.MediaKey.ShouldBe(mediaKey);
    }

    [Fact]
    public async Task ExecuteAsync_MediaFound_ReturnsProjectedOutput()
    {
        var mediaKey = Guid.NewGuid();
        var mediaTypeKey = Guid.NewGuid();
        var creatorKey = Guid.NewGuid();
        var writerKey = Guid.NewGuid();
        var media = MockPublishedContent(mediaKey, "Logo", "image", mediaTypeKey, creatorId: 5, writerId: 7);

        _cache.Setup(x => x.GetByIdAsync(mediaKey))
            .ReturnsAsync(media.Object);

        _userIdKeyResolver.Setup(x => x.TryGetAsync(5)).ReturnsAsync(Attempt<Guid>.Succeed(creatorKey));
        _userIdKeyResolver.Setup(x => x.TryGetAsync(7)).ReturnsAsync(Attempt<Guid>.Succeed(writerKey));

        var properties = new Dictionary<string, object?> { ["title"] = "Hello" };
        _normaliser.Setup(x => x.NormaliseProperties(media.Object, null))
            .Returns(properties);

        var context = CreateContext(new GetMediaSettings { MediaKey = mediaKey.ToString() });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();

        var output = result.OutputData as GetMediaOutput;
        output.ShouldNotBeNull();
        output.MediaKey.ShouldBe(mediaKey);
        output.Name.ShouldBe("Logo");
        output.MediaTypeAlias.ShouldBe("image");
        output.MediaTypeKey.ShouldBe(mediaTypeKey);
        output.CreatorKey.ShouldBe(creatorKey);
        output.WriterKey.ShouldBe(writerKey);
        output.Properties.ShouldContainKey("title");
        output.Properties["title"].ShouldBe("Hello");
    }

    [Fact]
    public async Task ExecuteAsync_DeletedUser_ReturnsNullCreatorKey()
    {
        var mediaKey = Guid.NewGuid();
        // Default resolver setup returns failed Attempt for any int — simulating a deleted user.
        var media = MockPublishedContent(mediaKey, "Logo", "image", Guid.NewGuid(), creatorId: 99, writerId: 99);

        _cache.Setup(x => x.GetByIdAsync(mediaKey))
            .ReturnsAsync(media.Object);

        var context = CreateContext(new GetMediaSettings { MediaKey = mediaKey.ToString() });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        var output = result.OutputData as GetMediaOutput;
        output!.CreatorKey.ShouldBeNull();
        output.WriterKey.ShouldBeNull();
    }

    // Same class of bug as Issue 205 on the content actions: reading published property
    // values off-request throws "Value cannot be null. (Parameter 'key2')" unless a
    // VariationContext is established for the duration of the read.
    [Fact]
    public async Task ExecuteAsync_VariantMedia_SetsVariationContextWhileReadingProperties()
    {
        var mediaKey = Guid.NewGuid();
        var media = MockVariantPublishedContent(mediaKey, "nl-NL");

        _cache.Setup(x => x.GetByIdAsync(mediaKey))
            .ReturnsAsync(media.Object);

        VariationContext? observed = null;
        _normaliser
            .Setup(x => x.NormaliseProperties(media.Object, "nl-NL"))
            .Callback(() => observed = _variationContextAccessor.VariationContext)
            .Returns(new Dictionary<string, object?>());

        var context = CreateContext(new GetMediaSettings
        {
            MediaKey = mediaKey.ToString(),
            Culture = "nl-NL",
        });

        await _action.ExecuteAsync(context, CancellationToken.None);

        observed.ShouldNotBeNull(
            "No VariationContext was in scope while property values were read.");
        observed.Culture.ShouldBe("nl-NL");
        observed.Segment.ShouldBe(string.Empty);
    }

    private static Mock<IPublishedContent> MockVariantPublishedContent(Guid key, string culture)
    {
        var media = MockPublishedContent(key, "Logo", "image", Guid.NewGuid());

        var mediaType = new Mock<IPublishedContentType>();
        mediaType.SetupGet(x => x.Alias).Returns("image");
        mediaType.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        mediaType.SetupGet(x => x.Variations).Returns(ContentVariation.Culture);
        mediaType.SetupGet(x => x.ItemType).Returns(PublishedItemType.Media);
        media.SetupGet(x => x.ContentType).Returns(mediaType.Object);

        media.SetupGet(x => x.Cultures).Returns(new Dictionary<string, PublishedCultureInfo>(StringComparer.OrdinalIgnoreCase)
        {
            [culture] = new PublishedCultureInfo(culture, "Logo", "logo", DateTime.UtcNow),
        });

        return media;
    }

    private static Mock<IPublishedContent> MockPublishedContent(
        Guid key,
        string name,
        string typeAlias,
        Guid mediaTypeKey,
        int creatorId = 0,
        int writerId = 0)
    {
        var mediaType = new Mock<IPublishedContentType>();
        mediaType.SetupGet(x => x.Alias).Returns(typeAlias);
        mediaType.SetupGet(x => x.Key).Returns(mediaTypeKey);
        mediaType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        mediaType.SetupGet(x => x.ItemType).Returns(PublishedItemType.Media);

        var media = new Mock<IPublishedContent>();
        media.SetupGet(x => x.Key).Returns(key);
        media.SetupGet(x => x.Name).Returns(name);
        media.SetupGet(x => x.ContentType).Returns(mediaType.Object);
        media.SetupGet(x => x.Cultures).Returns(new Dictionary<string, PublishedCultureInfo>(StringComparer.OrdinalIgnoreCase));
        media.SetupGet(x => x.Properties).Returns(Array.Empty<IPublishedProperty>());
        media.SetupGet(x => x.CreateDate).Returns(DateTime.UtcNow);
        media.SetupGet(x => x.UpdateDate).Returns(DateTime.UtcNow);
        media.SetupGet(x => x.CreatorId).Returns(creatorId);
        media.SetupGet(x => x.WriterId).Returns(writerId);
        return media;
    }

    private static ActionContext CreateContext(GetMediaSettings settings)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.getMedia",
            Settings = settings,
        };
}
