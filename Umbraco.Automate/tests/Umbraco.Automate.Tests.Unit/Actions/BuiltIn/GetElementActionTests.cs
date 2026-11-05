// STORY-2. Pending until T5: delete the LIBRARY_ELEMENT_ACTIONS guard and the Skip, then make it compile and pass.

#if LIBRARY_ELEMENT_ACTIONS
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class GetElementActionTests
{
    private const string ElementName = "G2 review rating";

    private static readonly DateTime CreateDate = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime UpdateDate = new(2026, 11, 2, 14, 30, 0, DateTimeKind.Utc);

    private readonly Mock<IPublishedElementCache> _cache = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IUserIdKeyResolver> _userIdKeyResolver = new();
    private readonly Mock<IContentValueNormaliser> _normaliser = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();

    // Real accessor, not a mock: IsAvailable == false makes it fall back to its AsyncLocal,
    // which is exactly what the accessor does on a background thread with no HTTP request.
    private readonly IVariationContextAccessor _variationContextAccessor =
        new HybridVariationContextAccessor(Mock.Of<IRequestCache>(c => c.IsAvailable == false));

    private readonly GetElementAction _action;

    public GetElementActionTests()
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
        _userIdKeyResolver
            .Setup(x => x.TryGetAsync(It.IsAny<int>()))
            .ReturnsAsync(Attempt<Guid>.Fail());

        // Default: element-level authorisation passes. Deny-path tests override it.
        _authorizer
            .Setup(a => a.AuthorizeElementAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _action = new GetElementAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _cache.Object,
            _contextFactory.Object,
            _userIdKeyResolver.Object,
            _normaliser.Object,
            _authorizer.Object,
            _variationContextAccessor,
            Mock.Of<ILogger<GetElementAction>>());
    }

    #region Happy path

    // STORY-2 AC1: Found
    [Fact(Skip = "Pending: T5")]
    public async Task ExecuteAsync_PublishedElement_TakesTheSuccessOutcome()
    {
        var elementKey = Guid.NewGuid();
        SetupCache(MockPublishedElement(elementKey));

        var result = await _action.ExecuteAsync(CreateContext(elementKey.ToString()), CancellationToken.None);

        // The default outcome is reported as no outcome, as for Get Content.
        (result.Status, result.Outcome).ShouldBe((ActionResultStatus.Success, null));
    }

    // STORY-2 AC2: Output
    [Fact(Skip = "Pending: T5")]
    public async Task ExecuteAsync_PublishedElement_ReturnsTheProjectedOutput()
    {
        var elementKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        var creatorKey = Guid.NewGuid();
        var writerKey = Guid.NewGuid();
        var element = MockPublishedElement(elementKey, contentTypeKey, creatorId: 5, writerId: 7);
        SetupCache(element);
        _userIdKeyResolver.Setup(x => x.TryGetAsync(5)).ReturnsAsync(Attempt<Guid>.Succeed(creatorKey));
        _userIdKeyResolver.Setup(x => x.TryGetAsync(7)).ReturnsAsync(Attempt<Guid>.Succeed(writerKey));
        var properties = new Dictionary<string, object?> { ["customSchema"] = "{\"@type\":\"AggregateRating\"}" };
        _normaliser.Setup(x => x.NormaliseProperties(element.Object, null)).Returns(properties);

        var result = await _action.ExecuteAsync(CreateContext(elementKey.ToString()), CancellationToken.None);

        result.OutputData.ShouldBeEquivalentTo(new GetElementOutput
        {
            ElementKey = elementKey,
            Name = ElementName,
            ContentTypeAlias = "schemaSnippet",
            ContentTypeKey = contentTypeKey,
            Culture = null,
            AvailableCultures = [],
            CreateDate = CreateDate,
            UpdateDate = UpdateDate,
            CreatorKey = creatorKey,
            WriterKey = writerKey,
            Properties = properties,
        });
    }

    // STORY-2 AC3: Culture given
    [Fact(Skip = "Pending: T5")]
    public async Task ExecuteAsync_CultureGiven_ReturnsThatCulturesProperties()
    {
        var elementKey = Guid.NewGuid();
        var element = MockVariantPublishedElement(elementKey, "en-US", "da-DK");
        SetupCache(element);
        _normaliser.Setup(x => x.NormaliseProperties(element.Object, "en-US"))
            .Returns(new Dictionary<string, object?> { ["customSchema"] = "English" });
        _normaliser.Setup(x => x.NormaliseProperties(element.Object, "da-DK"))
            .Returns(new Dictionary<string, object?> { ["customSchema"] = "Dansk" });

        var result = await _action.ExecuteAsync(CreateContext(elementKey.ToString(), "da-DK"), CancellationToken.None);

        ((GetElementOutput)result.OutputData!).Properties["customSchema"].ShouldBe("Dansk");
    }

    // STORY-2 AC4: Blank culture, variant element
    [Fact(Skip = "Pending: T5")]
    public async Task ExecuteAsync_BlankCultureOnVariantElement_UsesTheFirstCulture()
    {
        var elementKey = Guid.NewGuid();
        SetupCache(MockVariantPublishedElement(elementKey, "en-US", "da-DK"));

        var result = await _action.ExecuteAsync(CreateContext(elementKey.ToString(), culture: ""), CancellationToken.None);

        ((GetElementOutput)result.OutputData!).Culture.ShouldBe("en-US");
    }

    // STORY-2 AC5: Blank culture, invariant element
    [Fact(Skip = "Pending: T5")]
    public async Task ExecuteAsync_BlankCultureOnInvariantElement_ReturnsTheInvariantProperties()
    {
        var elementKey = Guid.NewGuid();
        var element = MockPublishedElement(elementKey);
        SetupCache(element);
        _normaliser.Setup(x => x.NormaliseProperties(element.Object, null))
            .Returns(new Dictionary<string, object?> { ["customSchema"] = "Invariant" });

        var result = await _action.ExecuteAsync(CreateContext(elementKey.ToString(), culture: ""), CancellationToken.None);

        ((GetElementOutput)result.OutputData!).Properties["customSchema"].ShouldBe("Invariant");
    }

    // STORY-2 AC6: Run log
    [Fact(Skip = "Pending: T5")]
    public async Task ExecuteAsync_PublishedElement_LogsOneLineNamingTheElement()
    {
        var elementKey = Guid.NewGuid();
        SetupCache(MockPublishedElement(elementKey));
        var context = CreateContext(elementKey.ToString());

        await _action.ExecuteAsync(context, CancellationToken.None);

        context.LogEntries.Select(e => e.Message.Contains(ActionLogFormat.Item(ElementName, elementKey)))
            .ShouldBe([true]);
    }

    #endregion

    #region Sad path

    // STORY-2 AC7: Not a GUID
    [Fact(Skip = "Pending: T5")]
    public async Task ExecuteAsync_ElementKeyNotAGuid_FailsWithValidationError()
    {
        var result = await _action.ExecuteAsync(CreateContext("abc"), CancellationToken.None);

        (result.Status, result.ErrorCategory).ShouldBe((ActionResultStatus.Failed, StepRunErrorCategory.Validation));
    }

    // STORY-2 AC8: Missing
    [Fact(Skip = "Pending: T5")]
    public async Task ExecuteAsync_AuthorizerReportsElementNotFound_TakesTheNotFoundOutcome()
    {
        var elementKey = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeElementAsync(elementKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.NotFound($"Element '{elementKey}' not found."));

        var result = await _action.ExecuteAsync(CreateContext(elementKey.ToString()), CancellationToken.None);

        (result.Status, result.Outcome).ShouldBe((ActionResultStatus.Success, GetElementAction.OutcomeNotFound));
    }

    // STORY-2 AC9: Trashed
    // A root-start-node account passes the authorizer for a trashed element, and the published
    // element cache drops trashed elements, so the read comes back empty. (IPublishedElement has
    // no Trashed flag at CMS 18.1.0; the cache is the only signal.)
    [Fact(Skip = "Pending: T5")]
    public async Task ExecuteAsync_ElementNotInPublishedCache_TakesTheNotFoundOutcome()
    {
        var elementKey = Guid.NewGuid();
        _cache.Setup(x => x.GetByIdAsync(elementKey, It.IsAny<bool?>()))
            .ReturnsAsync((IPublishedElement?)null);

        var result = await _action.ExecuteAsync(CreateContext(elementKey.ToString()), CancellationToken.None);

        (result.Status, result.Outcome).ShouldBe((ActionResultStatus.Success, GetElementAction.OutcomeNotFound));
    }

    // STORY-2 AC10: Permission refused
    [Fact(Skip = "Pending: T5")]
    public async Task ExecuteAsync_AuthorizationDenied_FailsWithPermissionError()
    {
        var elementKey = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeElementAsync(elementKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Service account lacks the required permissions (Umb.Element.Read)."));

        var result = await _action.ExecuteAsync(CreateContext(elementKey.ToString()), CancellationToken.None);

        (result.Status, result.ErrorCategory).ShouldBe((ActionResultStatus.Failed, StepRunErrorCategory.Authentication));
    }

    // STORY-2 AC10: Permission refused (the permission checked is Umb.Element.Read)
    [Fact(Skip = "Pending: T5")]
    public async Task ExecuteAsync_ChecksTheElementReadPermission()
    {
        var elementKey = Guid.NewGuid();
        SetupCache(MockPublishedElement(elementKey));

        await _action.ExecuteAsync(CreateContext(elementKey.ToString()), CancellationToken.None);

        _authorizer.Verify(
            a => a.AuthorizeElementAsync(
                elementKey,
                It.Is<IReadOnlySet<string>>(p => p.SetEquals(new[] { ActionElementBrowse.ActionLetter })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    private void SetupCache(Mock<IPublishedElement> element)
        => _cache.Setup(x => x.GetByIdAsync(element.Object.Key, It.IsAny<bool?>()))
            .ReturnsAsync(element.Object);

    private static Mock<IPublishedElement> MockVariantPublishedElement(Guid key, params string[] cultures)
    {
        var element = MockPublishedElement(key, variations: ContentVariation.Culture);

        var cultureInfos = new Dictionary<string, PublishedCultureInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in cultures)
        {
            cultureInfos[culture] = new PublishedCultureInfo(culture, ElementName, "g2-review-rating", UpdateDate);
        }

        element.SetupGet(x => x.Cultures).Returns(cultureInfos);
        return element;
    }

    private static Mock<IPublishedElement> MockPublishedElement(
        Guid key,
        Guid? contentTypeKey = null,
        int creatorId = 0,
        int writerId = 0,
        ContentVariation variations = ContentVariation.Nothing)
    {
        var contentType = new Mock<IPublishedContentType>();
        contentType.SetupGet(x => x.Alias).Returns("schemaSnippet");
        contentType.SetupGet(x => x.Key).Returns(contentTypeKey ?? Guid.NewGuid());
        contentType.SetupGet(x => x.Variations).Returns(variations);
        contentType.SetupGet(x => x.ItemType).Returns(PublishedItemType.Element);

        var element = new Mock<IPublishedElement>();
        element.SetupGet(x => x.Key).Returns(key);
        element.SetupGet(x => x.Name).Returns(ElementName);
        element.SetupGet(x => x.ContentType).Returns(contentType.Object);
        element.SetupGet(x => x.ItemType).Returns(PublishedItemType.Element);
        element.SetupGet(x => x.Cultures).Returns(new Dictionary<string, PublishedCultureInfo>(StringComparer.OrdinalIgnoreCase));
        element.SetupGet(x => x.Properties).Returns(Array.Empty<IPublishedProperty>());
        element.SetupGet(x => x.CreateDate).Returns(CreateDate);
        element.SetupGet(x => x.UpdateDate).Returns(UpdateDate);
        element.SetupGet(x => x.CreatorId).Returns(creatorId);
        element.SetupGet(x => x.WriterId).Returns(writerId);
        return element;
    }

    private static ActionContext CreateContext(string elementKey, string? culture = null)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.getElement",
            Settings = new GetElementSettings { ElementKey = elementKey, Culture = culture },
        };
}
#endif
