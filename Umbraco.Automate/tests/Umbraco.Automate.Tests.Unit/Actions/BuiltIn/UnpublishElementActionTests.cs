// STORY-6. Pending until T9: delete the LIBRARY_ELEMENT_ACTIONS guard and the Skip, then make it compile and pass.
#if LIBRARY_ELEMENT_ACTIONS
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class UnpublishElementActionTests
{
    private const string ElementName = "G2 review rating";

    private readonly Mock<IElementPublishingService> _publishingService = new();
    private readonly Mock<IElementService> _elementService = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();
    private readonly UnpublishElementAction _action;

    public UnpublishElementActionTests()
    {
        _contextFactory
            .Setup(x => x.EnsureUmbracoContext())
            .Returns(new UmbracoContextReference(
                Mock.Of<IUmbracoContext>(),
                isRoot: false,
                Mock.Of<IUmbracoContextAccessor>()));

        _authorizer
            .Setup(a => a.AuthorizeElementAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _publishingService
            .Setup(x => x.UnpublishAsync(It.IsAny<Guid>(), It.IsAny<ISet<string>?>(), It.IsAny<Guid>()))
            .ReturnsAsync(Attempt<ContentPublishingOperationStatus>.Succeed(ContentPublishingOperationStatus.Success));

        _action = new UnpublishElementAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _publishingService.Object,
            _elementService.Object,
            Mock.Of<IBackOfficeSecurityAccessor>(),
            _contextFactory.Object,
            _authorizer.Object,
            Mock.Of<ILogger<UnpublishElementAction>>());
    }

    #region Happy path

    // STORY-6 AC1: All cultures
    // For an invariant element the CMS wants no cultures at all (null).
    [Fact(Skip = "Pending: T9")]
    public async Task ExecuteAsync_CulturesEmptyOnInvariantElement_UnpublishesWithoutCultures()
    {
        var key = SetupElement(ContentVariation.Nothing);

        await _action.ExecuteAsync(CreateContext(new UnpublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        _publishingService.Verify(x => x.UnpublishAsync(key, null, It.IsAny<Guid>()), Times.Once);
    }

    // STORY-6 AC1: All cultures
    // For a culture-variant element the CMS refuses null (CannotPublishInvariantWhenVariant) and
    // reads "*" as every culture.
    [Fact(Skip = "Pending: T9")]
    public async Task ExecuteAsync_CulturesEmptyOnVariantElement_UnpublishesEveryCulture()
    {
        var key = SetupElement(ContentVariation.Culture);

        await _action.ExecuteAsync(CreateContext(new UnpublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        _publishingService.Verify(
            x => x.UnpublishAsync(key, It.Is<ISet<string>?>(s => s != null && s.SetEquals(new[] { "*" })), It.IsAny<Guid>()),
            Times.Once);
    }

    // STORY-6 AC1: All cultures
    [Fact(Skip = "Pending: T9")]
    public async Task ExecuteAsync_CulturesEmpty_Succeeds()
    {
        var key = SetupElement(ContentVariation.Nothing);

        var result = await _action.ExecuteAsync(CreateContext(new UnpublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
    }

    // STORY-6 AC2: One culture
    [Fact(Skip = "Pending: T9")]
    public async Task ExecuteAsync_OneCulture_UnpublishesOnlyThatCulture()
    {
        var key = SetupElement(ContentVariation.Culture);

        await _action.ExecuteAsync(
            CreateContext(new UnpublishElementSettings { ElementKey = key.ToString(), Cultures = "da-DK" }),
            CancellationToken.None);

        _publishingService.Verify(
            x => x.UnpublishAsync(key, It.Is<ISet<string>?>(s => s != null && s.SetEquals(new[] { "da-DK" })), It.IsAny<Guid>()),
            Times.Once);
    }

    // STORY-6 AC3: Audit entry
    // AuditTrailMiddleware writes the audit entry for every ICmsAction that succeeds.
    [Fact(Skip = "Pending: T9")]
    public void Action_IsACmsAction()
        => _action.ShouldBeAssignableTo<ICmsAction>();

    // STORY-6 AC4: Run log
    [Fact(Skip = "Pending: T9")]
    public async Task ExecuteAsync_UnpublishSucceeds_WritesOneRunLogLine()
    {
        var key = SetupElement(ContentVariation.Nothing);
        var context = CreateContext(new UnpublishElementSettings { ElementKey = key.ToString() });

        await _action.ExecuteAsync(context, CancellationToken.None);

        context.LogEntries.Count.ShouldBe(1);
    }

    // STORY-6 AC4: Run log
    // Unpublish Content logs only the key; the element action names the element too (R16).
    [Fact(Skip = "Pending: T9")]
    public async Task ExecuteAsync_UnpublishSucceeds_RunLogNamesTheElement()
    {
        var key = SetupElement(ContentVariation.Nothing);
        var context = CreateContext(new UnpublishElementSettings { ElementKey = key.ToString() });

        await _action.ExecuteAsync(context, CancellationToken.None);

        context.LogEntries[0].Message.ShouldContain($"'{ElementName}' ({key})");
    }

    #endregion

    #region Sad path

    // STORY-6 AC5: Not a GUID
    [Fact(Skip = "Pending: T9")]
    public async Task ExecuteAsync_ElementKeyIsNotAGuid_FailsWithValidationError()
    {
        var result = await _action.ExecuteAsync(CreateContext(new UnpublishElementSettings { ElementKey = "abc" }), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    // STORY-6 AC6: Missing element
    [Fact(Skip = "Pending: T9")]
    public async Task ExecuteAsync_AuthorizerReportsElementNotFound_Fails()
    {
        var key = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeElementAsync(key, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.NotFound($"Element '{key}' not found."));

        var result = await _action.ExecuteAsync(CreateContext(new UnpublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
    }

    // STORY-6 AC6: Missing element
    [Fact(Skip = "Pending: T9")]
    public async Task ExecuteAsync_ElementDoesNotExist_Fails()
    {
        var key = Guid.NewGuid();
        _elementService.Setup(x => x.GetById(key)).Returns((IElement?)null);

        var result = await _action.ExecuteAsync(CreateContext(new UnpublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
    }

    // STORY-6 AC7: Trashed element
    // A root Library start node passes the authorizer for an element in the recycle bin (#507).
    [Fact(Skip = "Pending: T9")]
    public async Task ExecuteAsync_ElementInRecycleBin_Fails()
    {
        var key = SetupElement(ContentVariation.Nothing, trashed: true);

        var result = await _action.ExecuteAsync(CreateContext(new UnpublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
    }

    // STORY-6 AC7: Trashed element
    [Fact(Skip = "Pending: T9")]
    public async Task ExecuteAsync_ElementInRecycleBin_NeverCallsUnpublish()
    {
        var key = SetupElement(ContentVariation.Nothing, trashed: true);

        await _action.ExecuteAsync(CreateContext(new UnpublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        _publishingService.Verify(x => x.UnpublishAsync(It.IsAny<Guid>(), It.IsAny<ISet<string>?>(), It.IsAny<Guid>()), Times.Never);
    }

    // STORY-6 AC8: Permission refused
    [Fact(Skip = "Pending: T9")]
    public async Task ExecuteAsync_AuthorizerDenies_FailsWithAuthenticationError()
    {
        var key = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeElementAsync(key, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Missing permission Umb.Element.Unpublish."));

        var result = await _action.ExecuteAsync(CreateContext(new UnpublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
    }

    // STORY-6 AC8: Permission refused
    // Unpublish Content asks for the publish letter; elements have their own unpublish permission.
    [Fact(Skip = "Pending: T9")]
    public async Task ExecuteAsync_AnyElement_AsksForTheUnpublishPermission()
    {
        var key = SetupElement(ContentVariation.Nothing);

        await _action.ExecuteAsync(CreateContext(new UnpublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        _authorizer.Verify(
            a => a.AuthorizeElementAsync(
                key,
                It.Is<IReadOnlySet<string>>(s => s.Contains(ActionElementUnpublish.ActionLetter)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    /// <summary>
    /// A published element named "G2 review rating" whose type varies as given.
    /// </summary>
    private Guid SetupElement(ContentVariation variations, bool trashed = false)
    {
        var key = Guid.NewGuid();

        var contentType = new Mock<ISimpleContentType>();
        contentType.SetupGet(x => x.Alias).Returns("schemaSnippet");
        contentType.SetupGet(x => x.Variations).Returns(variations);

        var element = new Mock<IElement>();
        element.SetupGet(x => x.Key).Returns(key);
        element.SetupGet(x => x.Name).Returns(ElementName);
        element.SetupGet(x => x.Trashed).Returns(trashed);
        element.SetupGet(x => x.Published).Returns(true);
        element.SetupGet(x => x.ContentType).Returns(contentType.Object);

        _elementService.Setup(x => x.GetById(key)).Returns(element.Object);
        return key;
    }

    private static ActionContext CreateContext(UnpublishElementSettings settings)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.unpublishElement",
            Settings = settings,
            ExecutionContext = new AutomationExecutionContext
            {
                ServiceAccountKey = Guid.NewGuid(),
                WorkspaceId = Guid.NewGuid(),
                WorkspaceName = "Test",
                AutomationId = Guid.NewGuid(),
                AutomationName = "Test",
                RunId = Guid.NewGuid(),
                InitiatorType = "test",
                AllowedConnections = [],
            },
        };
}
#endif
