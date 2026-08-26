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
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class GetMediaPropertyActionTests
{
    private readonly Mock<IPublishedMediaCache> _cache = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IContentValueNormaliser> _normaliser = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();

    // Real accessor, not a mock: IsAvailable == false makes it fall back to its AsyncLocal,
    // which is exactly what the accessor does on a background thread with no HTTP request.
    private readonly IVariationContextAccessor _variationContextAccessor =
        new HybridVariationContextAccessor(Mock.Of<IRequestCache>(c => c.IsAvailable == false));

    private readonly GetMediaPropertyAction _action;

    public GetMediaPropertyActionTests()
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

        _authorizer
            .Setup(a => a.AuthorizeMediaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _action = new GetMediaPropertyAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _cache.Object,
            _contextFactory.Object,
            _normaliser.Object,
            _authorizer.Object,
            _variationContextAccessor,
            Mock.Of<ILogger<GetMediaPropertyAction>>());
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.getMediaProperty");

    [Fact]
    public async Task ExecuteAsync_InvalidMediaKey_ReturnsValidationError()
    {
        var context = CreateContext(new GetMediaPropertySettings
        {
            MediaKey = "not-a-guid",
            PropertyAlias = "title",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyPropertyAlias_ReturnsValidationError()
    {
        var context = CreateContext(new GetMediaPropertySettings
        {
            MediaKey = Guid.NewGuid().ToString(),
            PropertyAlias = "",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_MediaNotFound_ReturnsNotFoundOutcome()
    {
        var mediaKey = Guid.NewGuid();
        _cache.Setup(x => x.GetByIdAsync(mediaKey))
            .ReturnsAsync((IPublishedContent?)null);

        var context = CreateContext(new GetMediaPropertySettings
        {
            MediaKey = mediaKey.ToString(),
            PropertyAlias = "title",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(GetMediaPropertyAction.OutcomeNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_PropertyNotFound_ReturnsPropertyNotFoundOutcome()
    {
        var mediaKey = Guid.NewGuid();
        var media = MockPublishedContent(mediaKey);
        // GetProperty returns null for any alias by default — that's the "not found" case.

        _cache.Setup(x => x.GetByIdAsync(mediaKey))
            .ReturnsAsync(media.Object);

        var context = CreateContext(new GetMediaPropertySettings
        {
            MediaKey = mediaKey.ToString(),
            PropertyAlias = "doesNotExist",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(GetMediaPropertyAction.OutcomePropertyNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_PropertyFound_ReturnsValue()
    {
        var mediaKey = Guid.NewGuid();
        var media = MockPublishedContent(mediaKey);

        var prop = new Mock<IPublishedProperty>();
        prop.SetupGet(x => x.Alias).Returns("title");
        media.Setup(x => x.GetProperty("title")).Returns(prop.Object);

        _cache.Setup(x => x.GetByIdAsync(mediaKey))
            .ReturnsAsync(media.Object);

        _normaliser.Setup(x => x.ReadProperty(media.Object, "title", null))
            .Returns("Hello");

        var context = CreateContext(new GetMediaPropertySettings
        {
            MediaKey = mediaKey.ToString(),
            PropertyAlias = "title",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();

        var output = result.OutputData as GetMediaPropertyOutput;
        output.ShouldNotBeNull();
        output.Value.ShouldBe("Hello");
        output.HasValue.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_SetsVariationContextWhileReadingTheProperty()
    {
        var mediaKey = Guid.NewGuid();
        var media = MockVariantPublishedContent(mediaKey, "nl-NL");

        var prop = new Mock<IPublishedProperty>();
        prop.SetupGet(x => x.Alias).Returns("title");
        media.Setup(x => x.GetProperty("title")).Returns(prop.Object);

        _cache.Setup(x => x.GetByIdAsync(mediaKey))
            .ReturnsAsync(media.Object);

        VariationContext? observed = null;
        _normaliser
            .Setup(x => x.ReadProperty(media.Object, "title", "nl-NL"))
            .Callback(() => observed = _variationContextAccessor.VariationContext)
            .Returns("Hallo");

        var context = CreateContext(new GetMediaPropertySettings
        {
            MediaKey = mediaKey.ToString(),
            PropertyAlias = "title",
            Culture = "nl-NL",
        });

        await _action.ExecuteAsync(context, CancellationToken.None);

        observed.ShouldNotBeNull(
            "No VariationContext was in scope while the property value was read.");
        observed.Culture.ShouldBe("nl-NL");
        observed.Segment.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_RestoresThePreviousVariationContext()
    {
        var previous = new VariationContext("da-DK");
        _variationContextAccessor.VariationContext = previous;

        var mediaKey = Guid.NewGuid();
        var media = MockVariantPublishedContent(mediaKey, "nl-NL");

        var prop = new Mock<IPublishedProperty>();
        prop.SetupGet(x => x.Alias).Returns("title");
        media.Setup(x => x.GetProperty("title")).Returns(prop.Object);

        _cache.Setup(x => x.GetByIdAsync(mediaKey))
            .ReturnsAsync(media.Object);

        _normaliser
            .Setup(x => x.ReadProperty(media.Object, "title", "nl-NL"))
            .Returns("Hallo");

        var context = CreateContext(new GetMediaPropertySettings
        {
            MediaKey = mediaKey.ToString(),
            PropertyAlias = "title",
            Culture = "nl-NL",
        });

        await _action.ExecuteAsync(context, CancellationToken.None);

        _variationContextAccessor.VariationContext.ShouldBeSameAs(previous);
    }

    private static Mock<IPublishedContent> MockVariantPublishedContent(Guid key, string culture)
    {
        var media = MockPublishedContent(key);

        var mediaType = new Mock<IPublishedContentType>();
        mediaType.SetupGet(x => x.Alias).Returns("image");
        mediaType.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        mediaType.SetupGet(x => x.Variations).Returns(ContentVariation.Culture);
        media.SetupGet(x => x.ContentType).Returns(mediaType.Object);

        media.SetupGet(x => x.Cultures).Returns(new Dictionary<string, PublishedCultureInfo>(StringComparer.OrdinalIgnoreCase)
        {
            [culture] = new PublishedCultureInfo(culture, "Test", "test", DateTime.UtcNow),
        });

        return media;
    }

    private static Mock<IPublishedContent> MockPublishedContent(Guid key)
    {
        var mediaType = new Mock<IPublishedContentType>();
        mediaType.SetupGet(x => x.Alias).Returns("image");
        mediaType.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        mediaType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);

        var media = new Mock<IPublishedContent>();
        media.SetupGet(x => x.Key).Returns(key);
        media.SetupGet(x => x.Name).Returns("Test");
        media.SetupGet(x => x.ContentType).Returns(mediaType.Object);
        media.SetupGet(x => x.Cultures).Returns(new Dictionary<string, PublishedCultureInfo>(StringComparer.OrdinalIgnoreCase));
        media.SetupGet(x => x.Properties).Returns(Array.Empty<IPublishedProperty>());
        return media;
    }

    private static ActionContext CreateContext(GetMediaPropertySettings settings)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.getMediaProperty",
            Settings = settings,
        };
}
