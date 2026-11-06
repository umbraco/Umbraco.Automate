using System.Data;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.ContentEditing.Validation;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class CreateElementActionTests
{
    private const string ElementName = "Acme – AggregateRating";
    private const string FolderName = "Reviews";
    private const int UserId = 42;

    private readonly Mock<IElementEditingService> _editingService = new();
    private readonly Mock<IElementContainerService> _containerService = new();
    private readonly Mock<IElementService> _elementService = new();
    private readonly Mock<IUserIdKeyResolver> _userIdKeyResolver = new();
    private readonly Mock<IContentTypeService> _contentTypeService = new();
    private readonly Mock<IBackOfficeSecurityAccessor> _securityAccessor = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();
    private readonly Mock<ICoreScopeProvider> _scopeProvider = new();
    private readonly Mock<ICoreScope> _scope = new();
    private readonly CreateElementAction _action;

    public CreateElementActionTests()
    {
        _contextFactory
            .Setup(x => x.EnsureUmbracoContext())
            .Returns(new UmbracoContextReference(
                Mock.Of<IUmbracoContext>(),
                isRoot: false,
                Mock.Of<IUmbracoContextAccessor>()));

        _authorizer
            .Setup(a => a.AuthorizeElementParentAsync(It.IsAny<Guid?>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _scopeProvider
            .Setup(p => p.CreateCoreScope(
                It.IsAny<IsolationLevel>(),
                It.IsAny<RepositoryCacheMode>(),
                It.IsAny<IEventDispatcher?>(),
                It.IsAny<IScopedNotificationPublisher?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns(_scope.Object);

        _userIdKeyResolver.Setup(x => x.GetAsync(It.IsAny<Guid>())).ReturnsAsync(UserId);
        _elementService
            .Setup(x => x.Save(It.IsAny<IElement>(), It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()))
            .Returns(new OperationResult(OperationResultType.Success, new EventMessages()));

        _action = new CreateElementAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _editingService.Object,
            _containerService.Object,
            _elementService.Object,
            _userIdKeyResolver.Object,
            _contentTypeService.Object,
            _securityAccessor.Object,
            _contextFactory.Object,
            _authorizer.Object,
            _scopeProvider.Object,
            Mock.Of<ILogger<CreateElementAction>>());
    }

    #region Happy path

    // STORY-7 AC1: In a folder
    [Fact]
    public async Task ExecuteAsync_InFolder_CreatesInTheFolder()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);

        await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        _editingService.Verify(x => x.CreateAsync(It.Is<ElementCreateModel>(m => m.ParentKey == folderKey), It.IsAny<Guid>()), Times.Once);
    }

    // STORY-7 AC1: In a folder
    [Fact]
    public async Task ExecuteAsync_InFolder_CreatesOfTheElementType()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);

        await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        _editingService.Verify(x => x.CreateAsync(It.Is<ElementCreateModel>(m => m.ContentTypeKey == type.Key), It.IsAny<Guid>()), Times.Once);
    }

    // STORY-7 AC1: In a folder
    [Fact]
    public async Task ExecuteAsync_InFolder_CreatesWithTheName()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);

        await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        _editingService.Verify(
            x => x.CreateAsync(It.Is<ElementCreateModel>(m => m.Variants.Single().Name == ElementName), It.IsAny<Guid>()),
            Times.Once);
    }

    // STORY-7 AC1: In a folder
    [Fact]
    public async Task ExecuteAsync_InFolder_Succeeds()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
    }

    // STORY-7 AC1: In a folder
    // No outcome on the result routes the step through its default exit, "success".
    [Fact]
    public async Task ExecuteAsync_InFolder_TakesTheDefaultOutcome()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        result.Outcome.ShouldBeNull();
    }

    // STORY-7 AC2: Output
    [Fact]
    public async Task ExecuteAsync_Created_OutputsTheElementKey()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        var created = SetupCreate(type);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        ((CreateElementOutput)result.OutputData!).ElementKey.ShouldBe(created.Key);
    }

    // STORY-7 AC2: Output
    [Fact]
    public async Task ExecuteAsync_Created_OutputsTheName()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        ((CreateElementOutput)result.OutputData!).Name.ShouldBe(ElementName);
    }

    // STORY-7 AC2: Output
    [Fact]
    public async Task ExecuteAsync_Created_OutputsTheContentTypeKey()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        ((CreateElementOutput)result.OutputData!).ContentTypeKey.ShouldBe(type.Key);
    }

    // STORY-7 AC2: Output
    [Fact]
    public async Task ExecuteAsync_Created_OutputsTheContentTypeAlias()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        ((CreateElementOutput)result.OutputData!).ContentTypeAlias.ShouldBe("schemaSnippet");
    }

    // STORY-7 AC2: Output
    [Fact]
    public async Task ExecuteAsync_Created_OutputsTheFolderKey()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        ((CreateElementOutput)result.OutputData!).FolderKey.ShouldBe(folderKey);
    }

    // STORY-7 AC3: At the root
    [Fact]
    public async Task ExecuteAsync_FolderEmpty_CreatesAtTheLibraryRoot()
    {
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);

        await _action.ExecuteAsync(CreateContext(Settings(folderKey: null, type.Key)), CancellationToken.None);

        _editingService.Verify(x => x.CreateAsync(It.Is<ElementCreateModel>(m => m.ParentKey == null), It.IsAny<Guid>()), Times.Once);
    }

    // STORY-7 AC3: At the root
    // The root is not a folder: it is checked as root access, never looked up as a key (R7).
    [Fact]
    public async Task ExecuteAsync_FolderEmpty_ChecksRootAccess()
    {
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);

        await _action.ExecuteAsync(CreateContext(Settings(folderKey: null, type.Key)), CancellationToken.None);

        _authorizer.Verify(
            a => a.AuthorizeElementParentAsync(null, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // STORY-7 AC3: At the root
    [Fact]
    public async Task ExecuteAsync_FolderEmpty_OutputsNoFolderKey()
    {
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey: null, type.Key)), CancellationToken.None);

        ((CreateElementOutput)result.OutputData!).FolderKey.ShouldBeNull();
    }

    // STORY-7 AC4: Property values
    [Fact]
    public async Task ExecuteAsync_PropertyValuesGiven_SetsTheValueOnTheCreatedElement()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        var created = SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = """{"customSchema": "{\"@type\":\"AggregateRating\"}"}""";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        created.GetValue("customSchema").ShouldBe("""{"@type":"AggregateRating"}""");
    }

    // STORY-7 AC4: Property values
    [Fact]
    public async Task ExecuteAsync_PropertyValuesGiven_SavesTheElementOnce()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        var created = SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = """{"customSchema": "x"}""";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        _elementService.Verify(x => x.Save(created, UserId, null), Times.Once);
    }

    // STORY-7 AC4: Property values
    // Values would go through each editor's backoffice-format conversion, so they stay out of the create model.
    [Fact]
    public async Task ExecuteAsync_PropertyValuesGiven_CreateModelHasNoPropertyValues()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = """{"customSchema": "x"}""";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        _editingService.Verify(x => x.CreateAsync(It.Is<ElementCreateModel>(m => !m.Properties.Any()), It.IsAny<Guid>()), Times.Once);
    }

    // STORY-7 AC4: Property values
    // Create Content passes an array or object value on as its raw JSON text.
    [Fact]
    public async Task ExecuteAsync_ArrayValueGiven_SetsTheRawJsonOnTheElement()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        var created = SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = """{"tags": ["a","b"]}""";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        created.GetValue("tags").ShouldBe("""["a","b"]""");
    }

    // STORY-7 AC4: Property values
    [Fact]
    public async Task ExecuteAsync_PropertiesJsonIsEmpty_DoesNotSave()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = "  ";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        _elementService.Verify(x => x.Save(It.IsAny<IElement>(), It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()), Times.Never);
    }

    // STORY-7 AC4: Property values
    [Fact]
    public async Task ExecuteAsync_SaveCancelled_FailsWithCancelledCategory()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);
        SetupSave(OperationResultType.FailedCancelledByEvent);
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = """{"customSchema": "x"}""";

        var result = await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Cancelled);
    }

    // STORY-7 AC4: Property values
    [Fact]
    public async Task ExecuteAsync_SaveFails_LogsTheElementWasNotCreated()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);
        SetupSave(OperationResultType.FailedCannot);
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = """{"customSchema": "x"}""";
        var context = CreateContext(settings);

        await _action.ExecuteAsync(context, CancellationToken.None);

        context.LogEntries.ShouldContain(e =>
            e.Level == ActionLogLevel.Error && e.Message == $"Library element '{ElementName}' was not created in folder '{FolderName}' because saving its property values failed");
    }

    // STORY-7 AC4: Property values
    // The create and the property save share one scope, so a failed save leaves no half-built element.
    [Fact]
    public async Task ExecuteAsync_CreateAndSaveSucceed_CompletesTheScope()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = """{"customSchema": "x"}""";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        _scope.Verify(s => s.Complete(), Times.Once);
    }

    // STORY-7 AC4: Property values
    [Fact]
    public async Task ExecuteAsync_PropertySaveFails_DoesNotCompleteTheScope()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);
        SetupSave(OperationResultType.FailedCannot);
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = """{"customSchema": "x"}""";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        _scope.Verify(s => s.Complete(), Times.Never);
    }

    // STORY-7 AC4: Property values
    [Fact]
    public async Task ExecuteAsync_NoPropertyValues_CompletesTheScope()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);

        await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        _scope.Verify(s => s.Complete(), Times.Once);
    }

    // STORY-7 AC4: Property values
    // Create Content matches aliases ignoring case; the CMS needs the property type's real alias.
    [Fact]
    public async Task ExecuteAsync_PropertyAliasDiffersInCase_SetsThePropertyUnderItsRealAlias()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        var created = SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = """{"CustomSchema": "x"}""";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        created.GetValue("customSchema").ShouldBe("x");
    }

    // STORY-7 AC5: Culture
    [Fact]
    public async Task ExecuteAsync_VariantTypeWithCulture_NamesTheCulture()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Culture);
        SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.Culture = "da-DK";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        _editingService.Verify(
            x => x.CreateAsync(
                It.Is<ElementCreateModel>(m => m.Variants.Any(v => v.Culture == "da-DK" && v.Name == ElementName)),
                It.IsAny<Guid>()),
            Times.Once);
    }

    // STORY-7 AC5: Culture
    [Fact]
    public async Task ExecuteAsync_VariantTypeWithCulture_SetsVariantPropertiesInTheCulture()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Culture);
        var created = SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.Culture = "da-DK";
        settings.PropertiesJson = """{"title": "Bedømmelse"}""";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        created.GetValue("title", "da-DK").ShouldBe("Bedømmelse");
    }

    // STORY-7 AC6: Bound folder
    // Bindings resolve before the action runs, so the action only ever sees the resolved key
    // (covered by ExecuteAsync_InFolder_CreatesInTheFolder). The field has to accept a binding.
    [Fact]
    public void FolderKeyField_SupportsBindings()
        => FolderKeyField().SupportsBindings.ShouldBeTrue();

    // STORY-7 AC7: Audit entry
    // AuditTrailMiddleware writes the audit entry for every ICmsAction that succeeds.
    [Fact]
    public void Action_IsACmsAction()
        => _action.ShouldBeAssignableTo<ICmsAction>();

    // STORY-7 AC8: Run log
    [Fact]
    public async Task ExecuteAsync_Created_WritesOneRunLogLine()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);
        var context = CreateContext(Settings(folderKey, type.Key));

        await _action.ExecuteAsync(context, CancellationToken.None);

        context.LogEntries.Count.ShouldBe(1);
    }

    // STORY-7 AC8: Run log
    [Fact]
    public async Task ExecuteAsync_Created_RunLogNamesTheNewElement()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        var created = SetupCreate(type);
        var context = CreateContext(Settings(folderKey, type.Key));

        await _action.ExecuteAsync(context, CancellationToken.None);

        context.LogEntries[0].Message.ShouldContain($"'{ElementName}' ({created.Key})");
    }

    #endregion

    #region Sad path

    // STORY-7 AC9: Folder not a GUID
    [Fact]
    public async Task ExecuteAsync_FolderIsNotAGuid_FailsWithValidationError()
    {
        var settings = new CreateElementSettings
        {
            FolderKey = "abc",
            ElementType = Guid.NewGuid().ToString(),
            Name = ElementName,
        };

        var result = await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    // STORY-7 AC10: Folder missing
    [Fact]
    public async Task ExecuteAsync_AuthorizerReportsFolderNotFound_TakesParentNotFoundOutcome()
    {
        var folderKey = SetupAuthorizerFolderNotFound();

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, Guid.NewGuid())), CancellationToken.None);

        result.Outcome.ShouldBe(CreateElementAction.OutcomeParentNotFound);
    }

    // STORY-7 AC10: Folder missing
    [Fact]
    public async Task ExecuteAsync_FolderDoesNotExist_TakesParentNotFoundOutcome()
    {
        var folderKey = Guid.NewGuid();
        _containerService.Setup(x => x.GetAsync(folderKey)).ReturnsAsync((EntityContainer?)null);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, Guid.NewGuid())), CancellationToken.None);

        result.Outcome.ShouldBe(CreateElementAction.OutcomeParentNotFound);
    }

    // STORY-7 AC10: Folder missing
    // The folder can go between the check and the create; the CMS then reports ParentNotFound.
    [Fact]
    public async Task ExecuteAsync_CmsReportsParentNotFound_TakesParentNotFoundOutcome()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreateFails(ContentEditingOperationStatus.ParentNotFound);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        result.Outcome.ShouldBe(CreateElementAction.OutcomeParentNotFound);
    }

    // STORY-7 AC11: Folder trashed, root account
    // A root Library start node passes the authorizer for a folder in the recycle bin, and
    // IElementEditingService.CreateAsync at 18.1.0 doesn't check Trashed on the folder, so the
    // action has to.
    [Fact]
    public async Task ExecuteAsync_FolderInRecycleBin_TakesParentNotFoundOutcome()
    {
        var folderKey = SetupFolder(trashed: true);
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        result.Outcome.ShouldBe(CreateElementAction.OutcomeParentNotFound);
    }

    // STORY-7 AC11: Folder trashed, root account
    [Fact]
    public async Task ExecuteAsync_FolderInRecycleBin_DoesNotCreate()
    {
        var folderKey = SetupFolder(trashed: true);
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);

        await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        _editingService.Verify(x => x.CreateAsync(It.IsAny<ElementCreateModel>(), It.IsAny<Guid>()), Times.Never);
    }

    // STORY-7 AC12: Folder trashed, scoped account
    // A folder start node can't see the bin, so the authorizer reports the trashed folder as not found.
    [Fact]
    public async Task ExecuteAsync_ScopedAccountAndFolderInRecycleBin_TakesParentNotFoundOutcome()
    {
        var folderKey = SetupFolder(trashed: true);
        _authorizer
            .Setup(a => a.AuthorizeElementParentAsync(folderKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.NotFound($"Element folder '{folderKey}' not found."));

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, Guid.NewGuid())), CancellationToken.None);

        result.Outcome.ShouldBe(CreateElementAction.OutcomeParentNotFound);
    }

    // STORY-7 AC13: Bound folder empty
    // The settings binder fails the step before the action runs when a field marked
    // BindingMustResolve resolves to empty, so nothing reaches the root.
    [Fact]
    public void FolderKeyField_BindingMustResolve()
        => FolderKeyField().BindingMustResolve.ShouldBeTrue();

    // STORY-7 AC14: Element type missing
    [Fact]
    public async Task ExecuteAsync_ElementTypeDoesNotExist_TakesElementTypeNotFoundOutcome()
    {
        var folderKey = SetupFolder();
        var typeKey = Guid.NewGuid();
        _contentTypeService.Setup(x => x.Get(typeKey)).Returns((IContentType?)null);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, typeKey)), CancellationToken.None);

        result.Outcome.ShouldBe(CreateElementAction.OutcomeElementTypeNotFound);
    }

    // STORY-7 AC14: Element type missing
    [Fact]
    public async Task ExecuteAsync_CmsReportsContentTypeNotFound_TakesElementTypeNotFoundOutcome()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreateFails(ContentEditingOperationStatus.ContentTypeNotFound);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        result.Outcome.ShouldBe(CreateElementAction.OutcomeElementTypeNotFound);
    }

    // STORY-7 AC15: Not allowed in the Library
    // CreateAsync reports NotAllowed for a type that isn't "Allow in Library" (ElementEditingService
    // TryGetAndValidateContentType), the same status it uses for a folder that refuses the type.
    [Fact]
    public async Task ExecuteAsync_TypeNotAllowedInLibrary_FailsWithValidationError()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing, allowedInLibrary: false);
        SetupCreateFails(ContentEditingOperationStatus.NotAllowed);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    // STORY-7 AC15: Not allowed in the Library
    [Fact]
    public async Task ExecuteAsync_TypeNotAllowedInLibrary_ErrorNamesTheType()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing, allowedInLibrary: false);
        SetupCreateFails(ContentEditingOperationStatus.NotAllowed);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        result.Exception!.Message.ShouldContain("schemaSnippet");
    }

    // STORY-7 AC16: Not allowed in the folder
    [Fact]
    public async Task ExecuteAsync_TypeNotAllowedInFolder_FailsWithValidationError()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing, alias: "promoBanner");
        SetupCreateFails(ContentEditingOperationStatus.NotAllowed);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    // STORY-7 AC16: Not allowed in the folder
    [Fact]
    public async Task ExecuteAsync_TypeNotAllowedInFolder_ErrorNamesTheType()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing, alias: "promoBanner");
        SetupCreateFails(ContentEditingOperationStatus.NotAllowed);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        result.Exception!.Message.ShouldContain("promoBanner");
    }

    // STORY-7 AC16: Not allowed in the folder
    [Fact]
    public async Task ExecuteAsync_TypeNotAllowedInFolder_ErrorNamesTheFolder()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing, alias: "promoBanner");
        SetupCreateFails(ContentEditingOperationStatus.NotAllowed);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        result.Exception!.Message.ShouldContain(FolderName);
    }

    // STORY-7 AC17: Invalid property values
    // Nothing is sent to CreateAsync but the name, so a PropertyValidationError status only
    // means mandatory properties aren't set yet; the draft is saved all the same.
    [Fact]
    public async Task ExecuteAsync_PropertyValidationError_StillSucceeds()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type, ContentEditingOperationStatus.PropertyValidationError, InvalidCustomSchema());

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
    }

    // STORY-7 AC17: Invalid property values
    [Fact]
    public async Task ExecuteAsync_PropertyValidationError_LogsNoWarning()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type, ContentEditingOperationStatus.PropertyValidationError, InvalidCustomSchema());
        var context = CreateContext(Settings(folderKey, type.Key));

        await _action.ExecuteAsync(context, CancellationToken.None);

        context.LogEntries.ShouldNotContain(e => e.Level == ActionLogLevel.Warning);
    }

    // STORY-7 AC17: Invalid property values
    // The element is saved as a draft holding the value, as Create Content does.
    [Fact]
    public async Task ExecuteAsync_ValueThatWouldFailValidation_IsSetOnTheElement()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        var created = SetupCreate(type, ContentEditingOperationStatus.PropertyValidationError, InvalidCustomSchema());
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = """{"customSchema": "not json"}""";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        created.GetValue("customSchema").ShouldBe("not json");
    }

    // Create Content parity: Authorization
    // A denied folder is not a missing one: it fails rather than taking parentNotFound.
    [Fact]
    public async Task ExecuteAsync_FolderAuthorizationDenied_FailsWithAuthenticationError()
    {
        var folderKey = SetupFolderAuthorizationDenied();

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, Guid.NewGuid())), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
    }

    // Create Content parity: Authorization
    [Fact]
    public async Task ExecuteAsync_FolderAuthorizationDenied_DoesNotCreate()
    {
        var folderKey = SetupFolderAuthorizationDenied();

        await _action.ExecuteAsync(CreateContext(Settings(folderKey, Guid.NewGuid())), CancellationToken.None);

        _editingService.Verify(x => x.CreateAsync(It.IsAny<ElementCreateModel>(), It.IsAny<Guid>()), Times.Never);
    }

    // STORY-1 AC8: Root access
    [Fact]
    public async Task ExecuteAsync_NoFolderAndRootDenied_FailsWithAuthenticationError()
    {
        _authorizer
            .Setup(a => a.AuthorizeElementParentAsync(null, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Service account is confined to a start node."));

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey: null, Guid.NewGuid())), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
    }

    // STORY-7 AC5: Culture
    [Fact]
    public async Task ExecuteAsync_VariantTypeWithoutCulture_FailsWithValidationError()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Culture);
        SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.Culture = "  ";

        var result = await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    // Create Content parity: Name required
    [Fact]
    public async Task ExecuteAsync_NameIsBlank_FailsWithValidationError()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.Name = " ";

        var result = await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    // R1: Element type required
    [Fact]
    public async Task ExecuteAsync_ElementTypeIsBlank_FailsWithValidationError()
    {
        var settings = Settings(SetupFolder(), Guid.NewGuid());
        settings.ElementType = "";

        var result = await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    // R1: Element type required
    [Fact]
    public async Task ExecuteAsync_ElementTypeIsNotAGuid_FailsWithValidationError()
    {
        var settings = Settings(SetupFolder(), Guid.NewGuid());
        settings.ElementType = "not-a-guid";

        var result = await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    // R1: Element type required
    // The picker stores its value comma-separated, with a trailing comma when one is chosen.
    [Fact]
    public async Task ExecuteAsync_ElementTypeHasTrailingComma_UsesTheKey()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.ElementType = $"{type.Key},";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        _editingService.Verify(x => x.CreateAsync(It.Is<ElementCreateModel>(m => m.ContentTypeKey == type.Key), It.IsAny<Guid>()), Times.Once);
    }

    // R1: Element type required
    [Fact]
    public async Task ExecuteAsync_ElementTypeHasSeveralKeys_UsesTheFirst()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.ElementType = $"{type.Key},{Guid.NewGuid()}";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        _editingService.Verify(x => x.CreateAsync(It.Is<ElementCreateModel>(m => m.ContentTypeKey == type.Key), It.IsAny<Guid>()), Times.Once);
    }

    // Create Content parity: Not an element type
    [Fact]
    public async Task ExecuteAsync_ContentTypeIsNotAnElementType_FailsWithValidationError()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing, isElement: false);
        SetupCreate(type);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    // Create Content parity: Not an element type
    [Fact]
    public async Task ExecuteAsync_ContentTypeIsNotAnElementType_ErrorSaysSo()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing, isElement: false);
        SetupCreate(type);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        result.Exception!.Message.ShouldContain("not an element type");
    }

    // Create Content parity: Not an element type
    [Fact]
    public async Task ExecuteAsync_ContentTypeIsNotAnElementType_DoesNotCreate()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing, isElement: false);
        SetupCreate(type);

        await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        _editingService.Verify(x => x.CreateAsync(It.IsAny<ElementCreateModel>(), It.IsAny<Guid>()), Times.Never);
    }

    // STORY-7 AC4: Property values
    [Fact]
    public async Task ExecuteAsync_PropertiesJsonIsMalformed_LogsAWarning()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = "{not json";
        var context = CreateContext(settings);

        await _action.ExecuteAsync(context, CancellationToken.None);

        context.LogEntries.ShouldContain(e => e.Level == ActionLogLevel.Warning && e.Message.Contains("not a valid JSON object"));
    }

    // STORY-7 AC4: Property values
    [Fact]
    public async Task ExecuteAsync_PropertiesJsonIsMalformed_StillCreates()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = "{not json";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        _editingService.Verify(x => x.CreateAsync(It.IsAny<ElementCreateModel>(), It.IsAny<Guid>()), Times.Once);
    }

    // STORY-7 AC4: Property values
    [Fact]
    public async Task ExecuteAsync_UnknownPropertyAlias_LogsAWarning()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = """{"doesNotExist": "x"}""";
        var context = CreateContext(settings);

        await _action.ExecuteAsync(context, CancellationToken.None);

        context.LogEntries.ShouldContain(e => e.Level == ActionLogLevel.Warning && e.Message.Contains("doesNotExist"));
    }

    // STORY-7 AC4: Property values
    [Fact]
    public async Task ExecuteAsync_UnknownPropertyAlias_IsSkipped()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = """{"doesNotExist": "x"}""";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        _elementService.Verify(x => x.Save(It.IsAny<IElement>(), It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()), Times.Never);
    }

    // STORY-7 AC4: Property values
    [Fact]
    public async Task ExecuteAsync_PropertiesJsonIsMalformed_DoesNotSaveAgain()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreate(type);
        var settings = Settings(folderKey, type.Key);
        settings.PropertiesJson = "{not json";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        _elementService.Verify(x => x.Save(It.IsAny<IElement>(), It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()), Times.Never);
    }

    // Create Content parity: Cancelled by a notification handler
    [Fact]
    public async Task ExecuteAsync_CancelledByNotification_FailsWithCancelledCategory()
    {
        var folderKey = SetupFolder();
        var type = SetupElementType(ContentVariation.Nothing);
        SetupCreateFails(ContentEditingOperationStatus.CancelledByNotification);

        var result = await _action.ExecuteAsync(CreateContext(Settings(folderKey, type.Key)), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Cancelled);
    }

    #endregion

    #region Settings

    // STORY-8 AC6
    [Fact]
    public void FolderKeyField_UsesTheElementKeyPicker()
        => FolderKeyField().EditorUiAlias.ShouldBe(Umbraco.Automate.Core.Constants.EditorUiAliases.ElementKeyPicker);

    // STORY-8 AC6
    [Fact]
    public void FolderKeyField_PicksFoldersOnly()
        => FolderKeyField().EditorConfig.ShouldContain("\"folderOnly\", \"value\": true");

    // STORY-8 AC8
    [Fact]
    public void ElementTypeField_OnlyPicksElementTypes()
    {
        var field = typeof(CreateElementSettings).GetProperty(nameof(CreateElementSettings.ElementType))!
            .GetCustomAttributes(typeof(FieldAttribute), false).Cast<FieldAttribute>().Single();

        field.EditorConfig.ShouldContain("\"onlyPickElementTypes\", \"value\": true");
    }

    // STORY-8 AC12: the CMS document type picker ignores a required field, so this one enforces it.
    [Fact]
    public void ElementTypeField_UsesTheDocumentTypePickerThatEnforcesRequired()
    {
        var field = typeof(CreateElementSettings).GetProperty(nameof(CreateElementSettings.ElementType))!
            .GetCustomAttributes(typeof(FieldAttribute), false).Cast<FieldAttribute>().Single();

        field.EditorUiAlias.ShouldBe(Umbraco.Automate.Core.Constants.EditorUiAliases.DocumentTypePicker);
    }

    #endregion

    private static FieldAttribute FolderKeyField()
        => typeof(CreateElementSettings).GetProperty(nameof(CreateElementSettings.FolderKey))!
            .GetCustomAttributes(typeof(FieldAttribute), false).Cast<FieldAttribute>().Single();

    private Guid SetupFolderAuthorizationDenied()
    {
        var folderKey = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeElementParentAsync(folderKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Out of start-node path."));
        return folderKey;
    }

    private Guid SetupAuthorizerFolderNotFound()
    {
        var folderKey = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeElementParentAsync(folderKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.NotFound($"Element folder '{folderKey}' not found."));
        return folderKey;
    }

    /// <summary>
    /// A Library folder named "Reviews".
    /// </summary>
    private Guid SetupFolder(bool trashed = false)
    {
        var folder = new EntityContainer(Constants.ObjectTypes.Element)
        {
            Key = Guid.NewGuid(),
            Name = FolderName,
            Trashed = trashed,
        };
        _containerService.Setup(x => x.GetAsync(folder.Key)).ReturnsAsync(folder);
        return folder.Key;
    }

    /// <summary>
    /// A real element type with an invariant "customSchema" property, plus a culture-variant
    /// "title" property when <paramref name="variations"/> includes culture.
    /// </summary>
    private ContentType SetupElementType(ContentVariation variations, string alias = "schemaSnippet", bool allowedInLibrary = true, bool isElement = true)
    {
        var shortStringHelper = new Mock<IShortStringHelper>();
        shortStringHelper
            .Setup(x => x.CleanString(It.IsAny<string>(), It.IsAny<CleanStringType>()))
            .Returns<string, CleanStringType>((text, _) => text);

        var type = new ContentType(shortStringHelper.Object, -1)
        {
            Alias = alias,
            Key = Guid.NewGuid(),
            Variations = variations,
            IsElement = isElement,
            AllowedInLibrary = allowedInLibrary,
        };
        type.AddPropertyType(new PropertyType(
            shortStringHelper.Object,
            Constants.PropertyEditors.Aliases.TextBox,
            ValueStorageType.Nvarchar,
            "customSchema"));

        type.AddPropertyType(new PropertyType(
            shortStringHelper.Object,
            Constants.PropertyEditors.Aliases.TextBox,
            ValueStorageType.Nvarchar,
            "tags"));

        if (variations.HasFlag(ContentVariation.Culture))
        {
            type.AddPropertyType(new PropertyType(
                shortStringHelper.Object,
                Constants.PropertyEditors.Aliases.TextBox,
                ValueStorageType.Nvarchar,
                "title")
            {
                Variations = ContentVariation.Culture,
            });
        }

        _contentTypeService.Setup(x => x.Get(type.Key)).Returns(type);
        return type;
    }

    /// <summary>
    /// CreateAsync creates and saves an element of <paramref name="type"/>, reporting
    /// <paramref name="status"/> on a successful attempt.
    /// </summary>
    private Element SetupCreate(
        ContentType type,
        ContentEditingOperationStatus status = ContentEditingOperationStatus.Success,
        ContentValidationResult? validationResult = null)
    {
        var created = new Element(ElementName, -1, type) { Key = Guid.NewGuid() };
        _editingService
            .Setup(x => x.CreateAsync(It.IsAny<ElementCreateModel>(), It.IsAny<Guid>()))
            .ReturnsAsync(Attempt.SucceedWithStatus(
                status,
                new ElementCreateResult { Content = created, ValidationResult = validationResult ?? new ContentValidationResult() }));
        return created;
    }

    private void SetupSave(OperationResultType status)
        => _elementService
            .Setup(x => x.Save(It.IsAny<IElement>(), It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()))
            .Returns(new OperationResult(status, new EventMessages()));

    private void SetupCreateFails(ContentEditingOperationStatus status)
        => _editingService
            .Setup(x => x.CreateAsync(It.IsAny<ElementCreateModel>(), It.IsAny<Guid>()))
            .ReturnsAsync(Attempt.FailWithStatus(status, new ElementCreateResult()));

    private static ContentValidationResult InvalidCustomSchema()
        => new()
        {
            ValidationErrors =
            [
                new PropertyValidationError
                {
                    JsonPath = "$.values[0].value",
                    ErrorMessages = ["Value is not valid JSON."],
                    Alias = "customSchema",
                    Culture = null,
                    Segment = null,
                },
            ],
        };

    private static CreateElementSettings Settings(Guid? folderKey, Guid elementTypeKey)
        => new()
        {
            FolderKey = folderKey?.ToString(),
            ElementType = elementTypeKey.ToString(),
            Name = ElementName,
        };

    private static ActionContext CreateContext(CreateElementSettings settings)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.createElement",
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
