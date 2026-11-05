// STORY-5. Pending until T8: delete the LIBRARY_ELEMENT_ACTIONS guard and the Skip, then make it compile and pass.
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
using Umbraco.Cms.Core.Models.ContentPublishing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class PublishElementActionTests
{
    private const string ElementName = "G2 review rating";

    private readonly Mock<IElementPublishingService> _publishingService = new();
    private readonly Mock<IElementService> _elementService = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();
    private readonly PublishElementAction _action;

    public PublishElementActionTests()
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

        _action = new PublishElementAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _publishingService.Object,
            _elementService.Object,
            Mock.Of<IBackOfficeSecurityAccessor>(),
            _contextFactory.Object,
            _authorizer.Object,
            Mock.Of<ILogger<PublishElementAction>>());
    }

    #region Happy path

    // STORY-5 AC1: Invariant publish
    // Same shape as PublishContentAction: one entry with a null culture, which the CMS reads as "*".
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_CulturesEmpty_PublishesTheInvariantCulture()
    {
        var key = SetupPublishableElement();

        await _action.ExecuteAsync(CreateContext(new PublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        _publishingService.Verify(
            x => x.PublishAsync(
                key,
                It.Is<ICollection<CulturePublishScheduleModel>>(c => c.Count == 1 && c.Single().Culture == null && c.Single().Schedule == null),
                It.IsAny<Guid>()),
            Times.Once);
    }

    // STORY-5 AC1: Invariant publish
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_CulturesEmpty_Succeeds()
    {
        var key = SetupPublishableElement();

        var result = await _action.ExecuteAsync(CreateContext(new PublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
    }

    // STORY-5 AC2: Some cultures
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_TwoCultures_PublishesBoth()
    {
        var key = SetupPublishableElement();

        await _action.ExecuteAsync(
            CreateContext(new PublishElementSettings { ElementKey = key.ToString(), Cultures = "en-US, da-DK" }),
            CancellationToken.None);

        _publishingService.Verify(
            x => x.PublishAsync(
                key,
                It.Is<ICollection<CulturePublishScheduleModel>>(c =>
                    c.Select(m => m.Culture).OrderBy(m => m).SequenceEqual(new[] { "da-DK", "en-US" })),
                It.IsAny<Guid>()),
            Times.Once);
    }

    // STORY-5 AC3: Output
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_PublishSucceeds_OutputsTheElementKey()
    {
        var key = SetupPublishableElement();

        var result = await _action.ExecuteAsync(
            CreateContext(new PublishElementSettings { ElementKey = key.ToString(), Cultures = "en-US,da-DK" }),
            CancellationToken.None);

        ((PublishElementOutput)result.OutputData!).ElementKey.ShouldBe(key);
    }

    // STORY-5 AC3: Output
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_PublishSucceeds_OutputsTheCultures()
    {
        var key = SetupPublishableElement();

        var result = await _action.ExecuteAsync(
            CreateContext(new PublishElementSettings { ElementKey = key.ToString(), Cultures = "en-US,da-DK" }),
            CancellationToken.None);

        ((PublishElementOutput)result.OutputData!).Cultures.ShouldBe(["en-US", "da-DK"], ignoreOrder: true);
    }

    // STORY-5 AC5: Audit entry
    // AuditTrailMiddleware writes the audit entry for every ICmsAction that succeeds.
    [Fact(Skip = "Pending: T8")]
    public void Action_IsACmsAction()
        => _action.ShouldBeAssignableTo<ICmsAction>();

    // STORY-5 AC6: Run log
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_PublishSucceeds_WritesOneRunLogLine()
    {
        var key = SetupPublishableElement();
        var context = CreateContext(new PublishElementSettings { ElementKey = key.ToString() });

        await _action.ExecuteAsync(context, CancellationToken.None);

        context.LogEntries.Count.ShouldBe(1);
    }

    // STORY-5 AC6: Run log
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_PublishSucceeds_RunLogNamesTheElement()
    {
        var key = SetupPublishableElement();
        var context = CreateContext(new PublishElementSettings { ElementKey = key.ToString() });

        await _action.ExecuteAsync(context, CancellationToken.None);

        context.LogEntries[0].Message.ShouldContain($"'{ElementName}' ({key})");
    }

    #endregion

    #region Sad path

    // STORY-5 AC7: Not a GUID
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_ElementKeyIsNotAGuid_FailsWithValidationError()
    {
        var result = await _action.ExecuteAsync(CreateContext(new PublishElementSettings { ElementKey = "abc" }), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    // STORY-5 AC8: Missing element
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_AuthorizerReportsElementNotFound_Fails()
    {
        var key = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeElementAsync(key, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.NotFound($"Element '{key}' not found."));

        var result = await _action.ExecuteAsync(CreateContext(new PublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
    }

    // STORY-5 AC8: Missing element
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_ElementDoesNotExist_Fails()
    {
        var key = Guid.NewGuid();
        _elementService.Setup(x => x.GetById(key)).Returns((IElement?)null);

        var result = await _action.ExecuteAsync(CreateContext(new PublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
    }

    // STORY-5 AC9: Trashed element
    // A root Library start node passes the authorizer for an element in the recycle bin (#507).
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_ElementInRecycleBin_Fails()
    {
        var key = SetupPublishableElement(trashed: true);

        var result = await _action.ExecuteAsync(CreateContext(new PublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
    }

    // STORY-5 AC9: Trashed element
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_ElementInRecycleBin_DoesNotPublish()
    {
        var key = SetupPublishableElement(trashed: true);

        await _action.ExecuteAsync(CreateContext(new PublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        _publishingService.Verify(
            x => x.PublishAsync(It.IsAny<Guid>(), It.IsAny<ICollection<CulturePublishScheduleModel>>(), It.IsAny<Guid>()),
            Times.Never);
    }

    // STORY-5 AC10: CMS refuses
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_MandatoryPropertyEmpty_FailsWithValidationError()
    {
        var key = SetupInvalidElement();

        var result = await _action.ExecuteAsync(CreateContext(new PublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    // STORY-5 AC10: CMS refuses
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_MandatoryPropertyEmpty_ErrorCarriesTheCmsReason()
    {
        var key = SetupInvalidElement();

        var result = await _action.ExecuteAsync(CreateContext(new PublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        result.Exception!.Message.ShouldContain(nameof(ContentPublishingOperationStatus.ContentInvalid));
    }

    // STORY-5 AC11: Permission refused
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_AuthorizerDenies_FailsWithAuthenticationError()
    {
        var key = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeElementAsync(key, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Missing permission Umb.Element.Publish."));

        var result = await _action.ExecuteAsync(CreateContext(new PublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
    }

    // STORY-5 AC11: Permission refused
    [Fact(Skip = "Pending: T8")]
    public async Task ExecuteAsync_AnyElement_AsksForThePublishPermission()
    {
        var key = SetupPublishableElement();

        await _action.ExecuteAsync(CreateContext(new PublishElementSettings { ElementKey = key.ToString() }), CancellationToken.None);

        _authorizer.Verify(
            a => a.AuthorizeElementAsync(
                key,
                It.Is<IReadOnlySet<string>>(s => s.Contains(ActionElementPublish.ActionLetter)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    private Mock<IElement> SetupElement(Guid key, bool trashed = false)
    {
        var element = new Mock<IElement>();
        element.SetupGet(x => x.Key).Returns(key);
        element.SetupGet(x => x.Name).Returns(ElementName);
        element.SetupGet(x => x.Trashed).Returns(trashed);
        element.SetupGet(x => x.ContentType).Returns(Mock.Of<ISimpleContentType>(t => t.Alias == "schemaSnippet"));
        _elementService.Setup(x => x.GetById(key)).Returns(element.Object);
        return element;
    }

    /// <summary>
    /// A live element the CMS publishes without complaint.
    /// </summary>
    private Guid SetupPublishableElement(bool trashed = false)
    {
        var key = Guid.NewGuid();
        var element = SetupElement(key, trashed);
        _publishingService
            .Setup(x => x.PublishAsync(key, It.IsAny<ICollection<CulturePublishScheduleModel>>(), It.IsAny<Guid>()))
            .ReturnsAsync(Attempt.SucceedWithStatus(
                ContentPublishingOperationStatus.Success,
                new ContentPublishingResult { Content = element.Object }));
        return key;
    }

    /// <summary>
    /// A live element whose mandatory "customSchema" is empty, so the CMS refuses to publish it.
    /// </summary>
    private Guid SetupInvalidElement()
    {
        var key = Guid.NewGuid();
        var element = SetupElement(key);
        _publishingService
            .Setup(x => x.PublishAsync(key, It.IsAny<ICollection<CulturePublishScheduleModel>>(), It.IsAny<Guid>()))
            .ReturnsAsync(Attempt.FailWithStatus(
                ContentPublishingOperationStatus.ContentInvalid,
                new ContentPublishingResult { Content = element.Object, InvalidPropertyAliases = ["customSchema"] }));
        return key;
    }

    private static ActionContext CreateContext(PublishElementSettings settings)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.publishElement",
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
