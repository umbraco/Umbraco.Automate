using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class UpdateElementPropertyActionTests
{
    private const string ElementName = "G2 review rating";

    private readonly Mock<IElementService> _elementService = new();
    private readonly Mock<IUserIdKeyResolver> _userIdKeyResolver = new();
    private readonly Mock<IBackOfficeSecurityAccessor> _securityAccessor = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();
    private readonly UpdateElementPropertyAction _action;

    public UpdateElementPropertyActionTests()
    {
        _userIdKeyResolver.Setup(x => x.GetAsync(It.IsAny<Guid>())).ReturnsAsync(-1);

        _contextFactory
            .Setup(x => x.EnsureUmbracoContext())
            .Returns(new UmbracoContextReference(
                Mock.Of<IUmbracoContext>(),
                isRoot: false,
                Mock.Of<IUmbracoContextAccessor>()));

        _authorizer
            .Setup(a => a.AuthorizeElementAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _elementService
            .Setup(x => x.Save(It.IsAny<IElement>(), It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()))
            .Returns(new OperationResult(OperationResultType.Success, new EventMessages()));

        _action = new UpdateElementPropertyAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _elementService.Object,
            _userIdKeyResolver.Object,
            _securityAccessor.Object,
            _contextFactory.Object,
            _authorizer.Object,
            Mock.Of<ILogger<UpdateElementPropertyAction>>());
    }

    #region Happy path

    // STORY-4 AC1: Value written
    [Fact]
    public async Task ExecuteAsync_PropertyExists_WritesTheNewValue()
    {
        var key = Guid.NewGuid();
        var element = SetupElement(key, ContentVariation.Nothing, "customSchema", "ratingCount");

        await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        element.Verify(x => x.SetValue("customSchema", "new", null, null), Times.Once);
    }

    // STORY-4 AC1: Value written
    [Fact]
    public async Task ExecuteAsync_PropertyExists_SavesTheElement()
    {
        var key = Guid.NewGuid();
        var element = SetupElement(key, ContentVariation.Nothing, "customSchema");

        await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        _elementService.Verify(x => x.Save(element.Object, It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()), Times.Once);
    }

    // STORY-4 AC2: Outcome
    [Fact]
    public async Task ExecuteAsync_WriteSucceeds_Succeeds()
    {
        var key = Guid.NewGuid();
        SetupElement(key, ContentVariation.Nothing, "customSchema");

        var result = await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
    }

    // STORY-4 AC2: Outcome
    // No outcome on the result routes the step through its default exit, "success".
    [Fact]
    public async Task ExecuteAsync_WriteSucceeds_TakesTheDefaultOutcome()
    {
        var key = Guid.NewGuid();
        SetupElement(key, ContentVariation.Nothing, "customSchema");

        var result = await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        result.Outcome.ShouldBeNull();
    }

    // STORY-4 AC3: Previous value
    [Fact]
    public async Task ExecuteAsync_WriteSucceeds_OutputsThePreviousValue()
    {
        var key = Guid.NewGuid();
        var element = SetupElement(key, ContentVariation.Nothing, "customSchema");
        element.Setup(x => x.GetValue("customSchema", null, null, false)).Returns("old");

        var result = await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        ((UpdateElementPropertyOutput)result.OutputData!).PreviousValue.ShouldBe("old");
    }

    // STORY-4 AC4: Other properties untouched
    [Fact]
    public async Task ExecuteAsync_WriteSucceeds_LeavesOtherPropertiesAlone()
    {
        var key = Guid.NewGuid();
        var element = SetupElement(key, ContentVariation.Nothing, "customSchema", "ratingCount");

        await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        element.Verify(x => x.SetValue("ratingCount", It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    // STORY-4 AC5: Published version untouched
    [Fact]
    public async Task ExecuteAsync_WriteSucceeds_DoesNotPublish()
    {
        var key = Guid.NewGuid();
        SetupElement(key, ContentVariation.Nothing, "customSchema");

        await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        _elementService.Verify(x => x.Publish(It.IsAny<IElement>(), It.IsAny<string[]>(), It.IsAny<int>()), Times.Never);
    }

    // STORY-4 AC5: Published version untouched
    [Fact]
    public async Task ExecuteAsync_WriteSucceeds_DoesNotSaveAndPublish()
    {
        var key = Guid.NewGuid();
        SetupElement(key, ContentVariation.Nothing, "customSchema");

        await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        _elementService.Verify(x => x.SaveAndPublish(It.IsAny<IElement>(), It.IsAny<string[]>(), It.IsAny<int>()), Times.Never);
    }

    // STORY-4 AC6: Culture and segment
    [Fact]
    public async Task ExecuteAsync_CultureAndSegmentGiven_WritesOnlyThatVariant()
    {
        var key = Guid.NewGuid();
        var element = SetupElement(key, ContentVariation.CultureAndSegment, "customSchema");

        await _action.ExecuteAsync(
            CreateContext(Settings(key, value: "new", culture: "da-DK", segment: "members")),
            CancellationToken.None);

        element.Verify(x => x.SetValue("customSchema", "new", "da-DK", "members"), Times.Once);
    }

    // STORY-4 AC7: Blank culture, variant element
    [Fact]
    public async Task ExecuteAsync_BlankCultureOnVariantElement_WritesTheFirstCulture()
    {
        var key = Guid.NewGuid();
        var element = SetupElement(key, ContentVariation.Culture, "customSchema");
        element.SetupGet(x => x.AvailableCultures).Returns(["en-US", "da-DK"]);

        await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        element.Verify(x => x.SetValue("customSchema", "new", "en-US", null), Times.Once);
    }

    // STORY-4 AC8: Bound key
    // Bindings resolve before the action runs, so the action only ever sees the resolved key.
    // The field has to accept a binding for that to happen.
    [Fact]
    public void ElementKeyField_SupportsBindings()
    {
        var field = typeof(UpdateElementPropertySettings).GetProperty(nameof(UpdateElementPropertySettings.ElementKey))!
            .GetCustomAttributes(typeof(FieldAttribute), false).Cast<FieldAttribute>().Single();

        field.SupportsBindings.ShouldBeTrue();
    }

    // STORY-8 AC3: Element picker
    [Fact]
    public void ElementKeyField_UsesTheElementPicker()
    {
        var field = typeof(UpdateElementPropertySettings).GetProperty(nameof(UpdateElementPropertySettings.ElementKey))!
            .GetCustomAttributes(typeof(FieldAttribute), false).Cast<FieldAttribute>().Single();

        field.EditorUiAlias.ShouldBe(Umbraco.Automate.Core.Constants.EditorUiAliases.ElementKeyPicker);
    }

    // STORY-4 AC9: Audit entry
    // AuditTrailMiddleware writes the audit entry for every ICmsAction that succeeds.
    [Fact]
    public void Action_IsACmsAction()
        => _action.ShouldBeAssignableTo<ICmsAction>();

    // STORY-4 AC10: Run log
    [Fact]
    public async Task ExecuteAsync_WriteSucceeds_WritesOneRunLogLine()
    {
        var key = Guid.NewGuid();
        SetupElement(key, ContentVariation.CultureAndSegment, "customSchema");
        var context = CreateContext(Settings(key, value: "new", culture: "da-DK", segment: "members"));

        await _action.ExecuteAsync(context, CancellationToken.None);

        context.LogEntries.Count.ShouldBe(1);
    }

    // STORY-4 AC10: Run log
    [Fact]
    public async Task ExecuteAsync_WriteSucceeds_RunLogNamesTheElement()
    {
        var key = Guid.NewGuid();
        SetupElement(key, ContentVariation.CultureAndSegment, "customSchema");
        var context = CreateContext(Settings(key, value: "new", culture: "da-DK", segment: "members"));

        await _action.ExecuteAsync(context, CancellationToken.None);

        context.LogEntries[0].Message.ShouldContain($"'{ElementName}' ({key})");
    }

    // STORY-4 AC10: Run log
    [Fact]
    public async Task ExecuteAsync_WriteSucceeds_RunLogNamesTheCultureAndSegment()
    {
        var key = Guid.NewGuid();
        SetupElement(key, ContentVariation.CultureAndSegment, "customSchema");
        var context = CreateContext(Settings(key, value: "new", culture: "da-DK", segment: "members"));

        await _action.ExecuteAsync(context, CancellationToken.None);

        context.LogEntries[0].Message.ShouldContain("da-DK / members");
    }

    #endregion

    #region Sad path

    // STORY-4 AC11: Not a GUID
    [Fact]
    public async Task ExecuteAsync_ElementKeyIsNotAGuid_FailsWithValidationError()
    {
        var context = CreateContext(new UpdateElementPropertySettings
        {
            ElementKey = "abc",
            PropertyAlias = "customSchema",
            Value = "new",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    // STORY-4 AC12: Missing element, outcome
    [Fact]
    public async Task ExecuteAsync_AuthorizerReportsElementNotFound_TakesNotFoundOutcome()
    {
        var key = SetupAuthorizerNotFound();

        var result = await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        result.Outcome.ShouldBe(UpdateElementPropertyAction.OutcomeNotFound);
    }

    // STORY-4 AC12: Missing element, outcome
    [Fact]
    public async Task ExecuteAsync_ElementDoesNotExist_TakesNotFoundOutcome()
    {
        var key = Guid.NewGuid();
        _elementService.Setup(x => x.GetById(key)).Returns((IElement?)null);

        var result = await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        result.Outcome.ShouldBe(UpdateElementPropertyAction.OutcomeNotFound);
    }

    // STORY-4 AC13: Missing element, nothing saved
    [Fact]
    public async Task ExecuteAsync_AuthorizerReportsElementNotFound_DoesNotSave()
    {
        var key = SetupAuthorizerNotFound();

        await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        _elementService.Verify(x => x.Save(It.IsAny<IElement>(), It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()), Times.Never);
    }

    // STORY-4 AC13: Missing element, nothing saved
    [Fact]
    public async Task ExecuteAsync_ElementDoesNotExist_DoesNotSave()
    {
        var key = Guid.NewGuid();
        _elementService.Setup(x => x.GetById(key)).Returns((IElement?)null);

        await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        _elementService.Verify(x => x.Save(It.IsAny<IElement>(), It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()), Times.Never);
    }

    // STORY-4 AC14: Trashed element, outcome
    // A root Library start node passes the authorizer for an element in the recycle bin (#507).
    [Fact]
    public async Task ExecuteAsync_ElementInRecycleBin_TakesNotFoundOutcome()
    {
        var key = Guid.NewGuid();
        var element = SetupElement(key, ContentVariation.Nothing, "customSchema");
        element.SetupGet(x => x.Trashed).Returns(true);

        var result = await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        result.Outcome.ShouldBe(UpdateElementPropertyAction.OutcomeNotFound);
    }

    // STORY-4 AC15: Trashed element, nothing saved
    [Fact]
    public async Task ExecuteAsync_ElementInRecycleBin_DoesNotSave()
    {
        var key = Guid.NewGuid();
        var element = SetupElement(key, ContentVariation.Nothing, "customSchema");
        element.SetupGet(x => x.Trashed).Returns(true);

        await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        _elementService.Verify(x => x.Save(It.IsAny<IElement>(), It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()), Times.Never);
    }

    // STORY-4 AC16: Missing property, outcome
    [Fact]
    public async Task ExecuteAsync_PropertyNotOnElementType_TakesPropertyNotFoundOutcome()
    {
        var key = Guid.NewGuid();
        SetupElement(key, ContentVariation.Nothing, "customSchema");

        var result = await _action.ExecuteAsync(CreateContext(Settings(key, value: "5", alias: "rating")), CancellationToken.None);

        result.Outcome.ShouldBe(UpdateElementPropertyAction.OutcomePropertyNotFound);
    }

    // STORY-4 AC17: Missing property, nothing saved
    [Fact]
    public async Task ExecuteAsync_PropertyNotOnElementType_DoesNotSave()
    {
        var key = Guid.NewGuid();
        SetupElement(key, ContentVariation.Nothing, "customSchema");

        await _action.ExecuteAsync(CreateContext(Settings(key, value: "5", alias: "rating")), CancellationToken.None);

        _elementService.Verify(x => x.Save(It.IsAny<IElement>(), It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()), Times.Never);
    }

    // STORY-4 AC18: Save cancelled
    [Fact]
    public async Task ExecuteAsync_SaveCancelledByNotification_FailsWithCancelledError()
    {
        var key = Guid.NewGuid();
        var element = SetupElement(key, ContentVariation.Nothing, "customSchema");
        _elementService
            .Setup(x => x.Save(element.Object, It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()))
            .Returns(new OperationResult(OperationResultType.FailedCancelledByEvent, new EventMessages()));

        var result = await _action.ExecuteAsync(CreateContext(Settings(key, value: "new")), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Cancelled);
    }

    #endregion

    private Guid SetupAuthorizerNotFound()
    {
        var key = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeElementAsync(key, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.NotFound($"Element '{key}' not found."));
        return key;
    }

    /// <summary>
    /// A live element named "G2 review rating" whose type has a property for each of
    /// <paramref name="propertyAliases"/>, all varying the same way as the type.
    /// </summary>
    private Mock<IElement> SetupElement(Guid key, ContentVariation variations, params string[] propertyAliases)
    {
        var contentType = new Mock<ISimpleContentType>();
        contentType.SetupGet(x => x.Alias).Returns("schemaSnippet");
        contentType.SetupGet(x => x.Variations).Returns(variations);

        var properties = new PropertyCollection(
            propertyAliases.Select(alias =>
            {
                var propertyType = new Mock<IPropertyType>();
                propertyType.SetupGet(x => x.Alias).Returns(alias);
                propertyType.SetupGet(x => x.Variations).Returns(variations);
                return new Property(propertyType.Object);
            }));

        var element = new Mock<IElement>();
        element.SetupGet(x => x.Key).Returns(key);
        element.SetupGet(x => x.Name).Returns(ElementName);
        element.SetupGet(x => x.ContentType).Returns(contentType.Object);
        element.SetupGet(x => x.Properties).Returns(properties);
        element.SetupGet(x => x.AvailableCultures).Returns([]);

        _elementService.Setup(x => x.GetById(key)).Returns(element.Object);
        return element;
    }

    private static UpdateElementPropertySettings Settings(
        Guid key,
        string? value,
        string alias = "customSchema",
        string? culture = null,
        string? segment = null)
        => new()
        {
            ElementKey = key.ToString(),
            PropertyAlias = alias,
            Value = value,
            Culture = culture,
            Segment = segment,
        };

    private static ActionContext CreateContext(UpdateElementPropertySettings settings)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.updateElementProperty",
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
