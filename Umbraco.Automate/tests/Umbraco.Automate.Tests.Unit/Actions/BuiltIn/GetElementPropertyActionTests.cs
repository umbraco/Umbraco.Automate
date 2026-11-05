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

public class GetElementPropertyActionTests
{
    private const string SchemaText = "{\"@type\":\"AggregateRating\",\"ratingValue\":4.6}";

    private readonly Mock<IPublishedElementCache> _cache = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IContentValueNormaliser> _normaliser = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();

    // Real accessor, not a mock: IsAvailable == false makes it fall back to its AsyncLocal,
    // which is exactly what the accessor does on a background thread with no HTTP request.
    private readonly IVariationContextAccessor _variationContextAccessor =
        new HybridVariationContextAccessor(Mock.Of<IRequestCache>(c => c.IsAvailable == false));

    private readonly GetElementPropertyAction _action;

    public GetElementPropertyActionTests()
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
            .Setup(a => a.AuthorizeElementAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _action = new GetElementPropertyAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _cache.Object,
            _contextFactory.Object,
            _normaliser.Object,
            _authorizer.Object,
            _variationContextAccessor,
            Mock.Of<ILogger<GetElementPropertyAction>>());
    }

    #region Happy path

    // STORY-3 AC1: Value present
    [Fact]
    public async Task ExecuteAsync_PropertyHasAValue_TakesTheSuccessOutcome()
    {
        var elementKey = Guid.NewGuid();
        var element = MockPublishedElementWithProperty(elementKey, "customSchema");
        _normaliser.Setup(x => x.ReadProperty(element.Object, "customSchema", null)).Returns(SchemaText);

        var result = await _action.ExecuteAsync(CreateContext(elementKey.ToString(), "customSchema"), CancellationToken.None);

        // The default outcome is reported as no outcome, as for Get Content Property.
        (result.Status, result.Outcome).ShouldBe((ActionResultStatus.Success, null));
    }

    // STORY-3 AC2: Output
    [Fact]
    public async Task ExecuteAsync_PropertyHasAValue_ReturnsTheProjectedOutput()
    {
        var elementKey = Guid.NewGuid();
        var element = MockPublishedElementWithProperty(elementKey, "customSchema");
        _normaliser.Setup(x => x.ReadProperty(element.Object, "customSchema", null)).Returns(SchemaText);

        var result = await _action.ExecuteAsync(CreateContext(elementKey.ToString(), "customSchema"), CancellationToken.None);

        result.OutputData.ShouldBeEquivalentTo(new GetElementPropertyOutput
        {
            ElementKey = elementKey,
            PropertyAlias = "customSchema",
            Culture = null,
            Value = SchemaText,
            HasValue = true,
        });
    }

    // STORY-3 AC3: Value empty
    // The normaliser returns null for an empty property, as for content; HasValue follows it.
    [Fact]
    public async Task ExecuteAsync_PropertyIsEmpty_ReportsNoValue()
    {
        var elementKey = Guid.NewGuid();
        var element = MockPublishedElementWithProperty(elementKey, "customSchema");
        _normaliser.Setup(x => x.ReadProperty(element.Object, "customSchema", null)).Returns((object?)null);

        var result = await _action.ExecuteAsync(CreateContext(elementKey.ToString(), "customSchema"), CancellationToken.None);

        ((GetElementPropertyOutput)result.OutputData!).HasValue.ShouldBeFalse();
    }

    // STORY-3 AC4: Culture given
    [Fact]
    public async Task ExecuteAsync_CultureGiven_ReturnsThatCulturesValue()
    {
        var elementKey = Guid.NewGuid();
        var element = MockPublishedElementWithProperty(elementKey, "customSchema", "en-US", "da-DK");
        _normaliser.Setup(x => x.ReadProperty(element.Object, "customSchema", "en-US")).Returns("English");
        _normaliser.Setup(x => x.ReadProperty(element.Object, "customSchema", "da-DK")).Returns("Dansk");

        var result = await _action.ExecuteAsync(CreateContext(elementKey.ToString(), "customSchema", "da-DK"), CancellationToken.None);

        ((GetElementPropertyOutput)result.OutputData!).Value.ShouldBe("Dansk");
    }

    #endregion

    #region Sad path

    // STORY-3 AC5: Not a GUID
    [Fact]
    public async Task ExecuteAsync_ElementKeyNotAGuid_FailsWithValidationError()
    {
        var result = await _action.ExecuteAsync(CreateContext("abc", "customSchema"), CancellationToken.None);

        (result.Status, result.ErrorCategory).ShouldBe((ActionResultStatus.Failed, StepRunErrorCategory.Validation));
    }

    // STORY-3 AC6: Missing element
    [Fact]
    public async Task ExecuteAsync_AuthorizerReportsElementNotFound_TakesTheNotFoundOutcome()
    {
        var elementKey = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeElementAsync(elementKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.NotFound($"Element '{elementKey}' not found."));

        var result = await _action.ExecuteAsync(CreateContext(elementKey.ToString(), "customSchema"), CancellationToken.None);

        (result.Status, result.Outcome).ShouldBe((ActionResultStatus.Success, GetElementPropertyAction.OutcomeNotFound));
    }

    // STORY-3 AC7: Trashed element
    // The published element cache drops trashed elements, so a root-start-node account that
    // passes the authorizer still reads nothing. A folder-start-node account gets NotFound from
    // the authorizer instead (STORY-1 AC10), which AC6's path already covers.
    [Fact]
    public async Task ExecuteAsync_ElementNotInPublishedCache_TakesTheNotFoundOutcome()
    {
        var elementKey = Guid.NewGuid();
        _cache.Setup(x => x.GetByIdAsync(elementKey, It.IsAny<bool?>()))
            .ReturnsAsync((IPublishedElement?)null);

        var result = await _action.ExecuteAsync(CreateContext(elementKey.ToString(), "customSchema"), CancellationToken.None);

        (result.Status, result.Outcome).ShouldBe((ActionResultStatus.Success, GetElementPropertyAction.OutcomeNotFound));
    }

    // STORY-3 AC8: Missing property
    [Fact]
    public async Task ExecuteAsync_ElementTypeHasNoSuchProperty_TakesThePropertyNotFoundOutcome()
    {
        var elementKey = Guid.NewGuid();
        // GetProperty returns null for any alias it wasn't set up for: that's the "no such property" case.
        MockPublishedElementWithProperty(elementKey, "customSchema");

        var result = await _action.ExecuteAsync(CreateContext(elementKey.ToString(), "rating"), CancellationToken.None);

        (result.Status, result.Outcome).ShouldBe((ActionResultStatus.Success, GetElementPropertyAction.OutcomePropertyNotFound));
    }

    #endregion

    private Mock<IPublishedElement> MockPublishedElementWithProperty(Guid key, string alias, params string[] cultures)
    {
        var contentType = new Mock<IPublishedContentType>();
        contentType.SetupGet(x => x.Alias).Returns("schemaSnippet");
        contentType.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        contentType.SetupGet(x => x.Variations).Returns(cultures.Length > 0 ? ContentVariation.Culture : ContentVariation.Nothing);
        contentType.SetupGet(x => x.ItemType).Returns(PublishedItemType.Element);

        var cultureInfos = new Dictionary<string, PublishedCultureInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in cultures)
        {
            cultureInfos[culture] = new PublishedCultureInfo(culture, "G2 review rating", "g2-review-rating", DateTime.UtcNow);
        }

        var property = new Mock<IPublishedProperty>();
        property.SetupGet(x => x.Alias).Returns(alias);

        var element = new Mock<IPublishedElement>();
        element.SetupGet(x => x.Key).Returns(key);
        element.SetupGet(x => x.Name).Returns("G2 review rating");
        element.SetupGet(x => x.ContentType).Returns(contentType.Object);
        element.SetupGet(x => x.ItemType).Returns(PublishedItemType.Element);
        element.SetupGet(x => x.Cultures).Returns(cultureInfos);
        element.SetupGet(x => x.Properties).Returns([property.Object]);
        element.Setup(x => x.GetProperty(alias)).Returns(property.Object);

        _cache.Setup(x => x.GetByIdAsync(key, It.IsAny<bool?>())).ReturnsAsync(element.Object);

        return element;
    }

    private static ActionContext CreateContext(string elementKey, string propertyAlias, string? culture = null)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.getElementProperty",
            Settings = new GetElementPropertySettings
            {
                ElementKey = elementKey,
                PropertyAlias = propertyAlias,
                Culture = culture,
            },
        };
}
