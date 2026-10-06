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
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class CreateContentActionTests
{
    private readonly Mock<IContentService> _contentService = new();
    private readonly Mock<IContentTypeService> _contentTypeService = new();
    private readonly Mock<IUserIdKeyResolver> _userIdKeyResolver = new();
    private readonly Mock<IBackOfficeSecurityAccessor> _securityAccessor = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();
    private readonly CreateContentAction _action;

    public CreateContentActionTests()
    {
        _userIdKeyResolver.Setup(x => x.GetAsync(It.IsAny<Guid>())).ReturnsAsync(-1);

        _contextFactory
            .Setup(x => x.EnsureUmbracoContext())
            .Returns(new UmbracoContextReference(
                Mock.Of<IUmbracoContext>(),
                isRoot: false,
                Mock.Of<IUmbracoContextAccessor>()));

        _authorizer
            .Setup(a => a.AuthorizeContentAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _authorizer
            .Setup(a => a.AuthorizeContentRootAsync(It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _action = new CreateContentAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _contentService.Object,
            _contentTypeService.Object,
            _userIdKeyResolver.Object,
            _securityAccessor.Object,
            _contextFactory.Object,
            _authorizer.Object,
            Mock.Of<ILogger<CreateContentAction>>());
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.createContent");

    [Fact]
    public async Task ExecuteAsync_EmptyContentType_ReturnsValidationError()
    {
        var context = CreateContext(new CreateContentSettings
        {
            ParentKey = Guid.NewGuid().ToString(),
            ContentType = "",
            Name = "New Page",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ContentTypeIsNotAKey_ReturnsValidationError()
    {
        var context = CreateContext(new CreateContentSettings
        {
            ParentKey = Guid.NewGuid().ToString(),
            ContentType = "page",
            Name = "New Page",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyName_ReturnsValidationError()
    {
        var context = CreateContext(new CreateContentSettings
        {
            ParentKey = Guid.NewGuid().ToString(),
            ContentType = Guid.NewGuid().ToString(),
            Name = "",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidParentKey_ReturnsValidationError()
    {
        var context = CreateContext(new CreateContentSettings
        {
            ParentKey = "not-a-guid",
            ContentType = Guid.NewGuid().ToString(),
            Name = "New Page",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_NoParent_CreatesAtTheContentRoot()
    {
        var contentTypeKey = Guid.NewGuid();
        var contentKey = Guid.NewGuid();

        var contentType = new Mock<IContentType>();
        contentType.SetupGet(x => x.Alias).Returns("page");
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        contentType.SetupGet(x => x.AllowedAsRoot).Returns(true);
        _contentTypeService.Setup(x => x.Get(contentTypeKey)).Returns(contentType.Object);

        var created = new Mock<IContent>();
        created.SetupGet(x => x.Key).Returns(contentKey);
        created.SetupGet(x => x.Properties).Returns(new PropertyCollection());

        _contentService
            .Setup(x => x.Create("New Page", Constants.System.Root, "page", -1))
            .Returns(created.Object);
        _contentService
            .Setup(x => x.Save(created.Object, It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()))
            .Returns(new OperationResult(OperationResultType.Success, new EventMessages()));

        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = null,
                ContentType = contentTypeKey.ToString(),
                Name = "New Page",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();

        // The root is not a node, so it must not be looked up or authorised as one.
        _contentService.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
        _authorizer.Verify(
            a => a.AuthorizeContentRootAsync(It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoParentAndRootDenied_FailsWithAuthenticationError()
    {
        _authorizer
            .Setup(a => a.AuthorizeContentRootAsync(It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Service account is confined to a start node."));

        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = "",
                ContentType = Guid.NewGuid().ToString(),
                Name = "New Page",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
    }

    [Fact]
    public async Task ExecuteAsync_NoParentAndTypeNotAllowedAtRoot_ReturnsValidationError()
    {
        var contentTypeKey = Guid.NewGuid();

        var contentType = new Mock<IContentType>();
        contentType.SetupGet(x => x.Alias).Returns("page");
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        contentType.SetupGet(x => x.AllowedAsRoot).Returns(false);
        _contentTypeService.Setup(x => x.Get(contentTypeKey)).Returns(contentType.Object);

        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = null,
                ContentType = contentTypeKey.ToString(),
                Name = "New Page",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);

        // Caught before creating, not left to throw at save time.
        _contentService.Verify(
            x => x.Create(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_AuthorizerReportsParentNotFound_SucceedsWithNotFoundOutcome()
    {
        var key = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeContentAsync(key, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.NotFound($"Content node '{key}' not found."));

        var result = await _action.ExecuteAsync(CreateContext(new CreateContentSettings { ParentKey = key.ToString(), ContentType = Guid.NewGuid().ToString(), Name = "New Page" }, Guid.NewGuid()), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
    }

    [Fact]
    public async Task ExecuteAsync_AuthorizerReportsParentNotFound_EmitsNotFoundOutcome()
    {
        var key = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeContentAsync(key, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.NotFound($"Content node '{key}' not found."));

        var result = await _action.ExecuteAsync(CreateContext(new CreateContentSettings { ParentKey = key.ToString(), ContentType = Guid.NewGuid().ToString(), Name = "New Page" }, Guid.NewGuid()), CancellationToken.None);

        result.Outcome.ShouldBe("parentNotFound");
    }

    [Fact]
    public async Task ExecuteAsync_AuthorizerReportsParentNotFound_DoesNotCreate()
    {
        var key = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeContentAsync(key, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.NotFound($"Content node '{key}' not found."));

        await _action.ExecuteAsync(CreateContext(new CreateContentSettings { ParentKey = key.ToString(), ContentType = Guid.NewGuid().ToString(), Name = "New Page" }, Guid.NewGuid()), CancellationToken.None);

        _contentService.Verify(x => x.Create(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ParentAuthorizationDenied_ReturnsAuthenticationError()
    {
        var key = Guid.NewGuid();
        _authorizer
            .Setup(a => a.AuthorizeContentAsync(key, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Out of start-node path."));

        var result = await _action.ExecuteAsync(CreateContext(new CreateContentSettings { ParentKey = key.ToString(), ContentType = Guid.NewGuid().ToString(), Name = "New Page" }, Guid.NewGuid()), CancellationToken.None);

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
    }

    [Fact]
    public async Task ExecuteAsync_ParentNotFound_ReturnsParentNotFoundOutcome()
    {
        var parentKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        _contentService.Setup(x => x.GetById(parentKey)).Returns((IContent?)null);

        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = parentKey.ToString(),
                ContentType = contentTypeKey.ToString(),
                Name = "New Page",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(CreateContentAction.OutcomeParentNotFound);

        var output = result.OutputData as CreateContentOutput;
        output.ShouldNotBeNull();
        output.ContentTypeKey.ShouldBe(contentTypeKey);
    }

    [Fact]
    public async Task ExecuteAsync_ContentTypeNotFound_ReturnsContentTypeNotFoundOutcome()
    {
        var parentKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        _contentService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IContent>());
        _contentTypeService.Setup(x => x.Get(contentTypeKey)).Returns((IContentType?)null);

        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = parentKey.ToString(),
                ContentType = contentTypeKey.ToString(),
                Name = "New Page",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(CreateContentAction.OutcomeContentTypeNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_VariantContentTypeWithoutCulture_ReturnsValidationError()
    {
        var parentKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        _contentService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IContent>());

        var contentType = new Mock<IContentType>();
        contentType.SetupGet(x => x.Alias).Returns("page");
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Culture);
        _contentTypeService.Setup(x => x.Get(contentTypeKey)).Returns(contentType.Object);

        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = parentKey.ToString(),
                ContentType = contentTypeKey.ToString(),
                Name = "New Page",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_CreatesAndSavesContent()
    {
        var parentKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        var contentKey = Guid.NewGuid();
        _contentService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IContent>());

        var contentType = new Mock<IContentType>();
        contentType.SetupGet(x => x.Alias).Returns("page");
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        _contentTypeService.Setup(x => x.Get(contentTypeKey)).Returns(contentType.Object);

        var created = new Mock<IContent>();
        created.SetupGet(x => x.Key).Returns(contentKey);
        created.SetupGet(x => x.Properties).Returns(new PropertyCollection());

        _contentService
            .Setup(x => x.Create("New Page", parentKey, "page", -1))
            .Returns(created.Object);
        _contentService
            .Setup(x => x.Save(created.Object, It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()))
            .Returns(new OperationResult(OperationResultType.Success, new EventMessages()));

        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = parentKey.ToString(),
                ContentType = contentTypeKey.ToString(),
                Name = "New Page",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();

        var output = result.OutputData as CreateContentOutput;
        output.ShouldNotBeNull();
        output.ContentKey.ShouldBe(contentKey);
        output.Name.ShouldBe("New Page");
        output.ContentTypeKey.ShouldBe(contentTypeKey);
        output.ContentTypeAlias.ShouldBe("page");
        output.ParentKey.ShouldBe(parentKey);
    }

    [Fact]
    public async Task ExecuteAsync_PickerValueWithTrailingComma_ResolvesTheContentType()
    {
        var parentKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        _contentService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IContent>());

        var contentType = new Mock<IContentType>();
        contentType.SetupGet(x => x.Alias).Returns("page");
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        _contentTypeService.Setup(x => x.Get(contentTypeKey)).Returns(contentType.Object);

        var created = new Mock<IContent>();
        created.SetupGet(x => x.Properties).Returns(new PropertyCollection());

        _contentService
            .Setup(x => x.Create("New Page", parentKey, "page", -1))
            .Returns(created.Object);
        _contentService
            .Setup(x => x.Save(created.Object, It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()))
            .Returns(new OperationResult(OperationResultType.Success, new EventMessages()));

        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = parentKey.ToString(),
                ContentType = $"{contentTypeKey},",
                Name = "New Page",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_SaveCancelledByEvent_MapsToCancelled()
    {
        var parentKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        _contentService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IContent>());

        var contentType = new Mock<IContentType>();
        contentType.SetupGet(x => x.Alias).Returns("page");
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        _contentTypeService.Setup(x => x.Get(contentTypeKey)).Returns(contentType.Object);

        var created = new Mock<IContent>();
        created.SetupGet(x => x.Properties).Returns(new PropertyCollection());

        _contentService
            .Setup(x => x.Create("New Page", parentKey, "page", -1))
            .Returns(created.Object);
        _contentService
            .Setup(x => x.Save(created.Object, It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()))
            .Returns(new OperationResult(OperationResultType.FailedCancelledByEvent, new EventMessages()));

        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = parentKey.ToString(),
                ContentType = contentTypeKey.ToString(),
                Name = "New Page",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Cancelled);
    }

    [Fact]
    public async Task ExecuteAsync_VariantContentType_SetsCultureVariantPropertiesForTheCulture()
    {
        var parentKey = Guid.NewGuid();
        var created = SetupRealCreate(parentKey, ContentVariation.Culture, out var contentTypeKey);

        var result = await _action.ExecuteAsync(
            CreateContext(
                new CreateContentSettings
                {
                    ParentKey = parentKey.ToString(),
                    ContentType = contentTypeKey.ToString(),
                    Name = "New Page",
                    Culture = "en-US",
                    PropertiesJson = """{ "title": "Hello", "code": "ABC" }""",
                },
                Guid.NewGuid()),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        created.GetValue<string>("title", "en-US").ShouldBe("Hello");
        created.GetValue<string>("code").ShouldBe("ABC");
        _contentService.Verify(x => x.Save(created, It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_InvariantContentType_IgnoresCultureWhenSettingProperties()
    {
        var parentKey = Guid.NewGuid();
        var created = SetupRealCreate(parentKey, ContentVariation.Nothing, out var contentTypeKey);

        var result = await _action.ExecuteAsync(
            CreateContext(
                new CreateContentSettings
                {
                    ParentKey = parentKey.ToString(),
                    ContentType = contentTypeKey.ToString(),
                    Name = "New Page",
                    Culture = "en-US",
                    PropertiesJson = """{ "code": "ABC" }""",
                },
                Guid.NewGuid()),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        created.GetValue<string>("code").ShouldBe("ABC");
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_LogsWhatWasCreatedAndWhere()
    {
        var parentKey = Guid.NewGuid();
        var created = SetupRealCreate(parentKey, ContentVariation.Culture, out var contentTypeKey);
        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = parentKey.ToString(),
                ContentType = contentTypeKey.ToString(),
                Name = "New Page",
                Culture = "en-US",
            },
            Guid.NewGuid());

        await _action.ExecuteAsync(context, CancellationToken.None);

        var entry = context.LogEntries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(ActionLogLevel.Info);
        entry.Message.ShouldBe($"Created 'New Page' ({created.Key}) as page under {Guid.Empty} in en-US");
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPropertyAlias_LogsWarningAndStillCreates()
    {
        var parentKey = Guid.NewGuid();
        SetupRealCreate(parentKey, ContentVariation.Nothing, out var contentTypeKey);
        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = parentKey.ToString(),
                ContentType = contentTypeKey.ToString(),
                Name = "New Page",
                PropertiesJson = """{ "code": "ABC", "missing": "secret value" }""",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        context.LogEntries.Count.ShouldBe(2);
        context.LogEntries[0].Level.ShouldBe(ActionLogLevel.Warning);
        context.LogEntries[0].Message.ShouldBe("Property 'missing' does not exist on page and was skipped");
        context.LogEntries[1].Message.ShouldStartWith("Created 'New Page'");
        context.LogEntries.ShouldAllBe(e => !e.Message.Contains("secret value"));
    }

    [Fact]
    public async Task ExecuteAsync_ParentNotFound_LogsWarning()
    {
        var parentKey = Guid.NewGuid();
        _contentService.Setup(x => x.GetById(parentKey)).Returns((IContent?)null);
        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = parentKey.ToString(),
                ContentType = Guid.NewGuid().ToString(),
                Name = "New Page",
            },
            Guid.NewGuid());

        await _action.ExecuteAsync(context, CancellationToken.None);

        var entry = context.LogEntries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(ActionLogLevel.Warning);
        entry.Message.ShouldBe($"Parent content {parentKey} was not found, so nothing was created");
    }

    /// <summary>
    /// Sets up a create backed by real CMS domain objects rather than mocks, so SetValue runs
    /// the CMS's own variation checks. The type has an invariant "code" property, plus a
    /// culture-variant "title" property when <paramref name="variations"/> includes culture.
    /// </summary>
    private Content SetupRealCreate(Guid parentKey, ContentVariation variations, out Guid contentTypeKey)
    {
        var typeKey = Guid.NewGuid();
        contentTypeKey = typeKey;
        _contentService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IContent>());

        var shortStringHelper = new Mock<IShortStringHelper>();
        shortStringHelper
            .Setup(x => x.CleanString(It.IsAny<string>(), It.IsAny<CleanStringType>()))
            .Returns<string, CleanStringType>((text, _) => text);

        var contentType = new ContentType(shortStringHelper.Object, -1)
        {
            Alias = "page",
            Key = typeKey,
            Variations = variations,
        };
        contentType.AddPropertyType(new PropertyType(
            shortStringHelper.Object,
            Constants.PropertyEditors.Aliases.TextBox,
            ValueStorageType.Nvarchar,
            "code"));

        if (variations.HasFlag(ContentVariation.Culture))
        {
            contentType.AddPropertyType(new PropertyType(
                shortStringHelper.Object,
                Constants.PropertyEditors.Aliases.TextBox,
                ValueStorageType.Nvarchar,
                "title")
            {
                Variations = ContentVariation.Culture,
            });
        }

        _contentTypeService.Setup(x => x.Get(typeKey)).Returns(contentType);

        var created = new Content("New Page", -1, contentType);
        _contentService.Setup(x => x.Create("New Page", parentKey, "page", -1)).Returns(created);
        _contentService
            .Setup(x => x.Save(created, It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()))
            .Returns(new OperationResult(OperationResultType.Success, new EventMessages()));

        return created;
    }

    [Fact]
    public async Task ExecuteAsync_PropertiesJsonIsMalformed_SavesWithoutSettingValues()
    {
        var parentKey = Guid.NewGuid();
        var created = SetupInvariantCreate(parentKey, out var contentTypeKey, "title");

        var result = await _action.ExecuteAsync(
            CreateContext(
                new CreateContentSettings
                {
                    ParentKey = parentKey.ToString(),
                    ContentType = contentTypeKey.ToString(),
                    Name = "New Page",
                    PropertiesJson = "{ not json",
                },
                Guid.NewGuid()),
            CancellationToken.None);

        // Property values are optional convenience config: bad JSON is skipped, not a failure.
        result.Status.ShouldBe(ActionResultStatus.Success);
        created.Verify(
            x => x.SetValue(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
        _contentService.Verify(
            x => x.Save(created.Object, It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PropertiesJsonHasUnknownAlias_SkipsIt()
    {
        var parentKey = Guid.NewGuid();
        var created = SetupInvariantCreate(parentKey, out var contentTypeKey, "title");

        var result = await _action.ExecuteAsync(
            CreateContext(
                new CreateContentSettings
                {
                    ParentKey = parentKey.ToString(),
                    ContentType = contentTypeKey.ToString(),
                    Name = "New Page",
                    PropertiesJson = """{ "doesNotExist": "value" }""",
                },
                Guid.NewGuid()),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        created.Verify(
            x => x.SetValue(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PropertiesJsonHasKnownAlias_SetsTheValue()
    {
        var parentKey = Guid.NewGuid();
        var created = SetupInvariantCreate(parentKey, out var contentTypeKey, "title");

        var result = await _action.ExecuteAsync(
            CreateContext(
                new CreateContentSettings
                {
                    ParentKey = parentKey.ToString(),
                    ContentType = contentTypeKey.ToString(),
                    Name = "New Page",
                    PropertiesJson = """{ "title": "Hello", "doesNotExist": "ignored" }""",
                },
                Guid.NewGuid()),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        created.Verify(x => x.SetValue("title", "Hello", null, null), Times.Once);
        created.Verify(
            x => x.SetValue("doesNotExist", It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_VariantContentTypeWithCulture_SetsTheCultureName()
    {
        var parentKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        _contentService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IContent>());

        var contentType = new Mock<IContentType>();
        contentType.SetupGet(x => x.Alias).Returns("page");
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Culture);
        _contentTypeService.Setup(x => x.Get(contentTypeKey)).Returns(contentType.Object);

        var created = new Mock<IContent>();
        created.SetupGet(x => x.Properties).Returns(new PropertyCollection());
        _contentService.Setup(x => x.Create("New Page", parentKey, "page", -1)).Returns(created.Object);
        _contentService
            .Setup(x => x.Save(created.Object, It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()))
            .Returns(new OperationResult(OperationResultType.Success, new EventMessages()));

        var result = await _action.ExecuteAsync(
            CreateContext(
                new CreateContentSettings
                {
                    ParentKey = parentKey.ToString(),
                    ContentType = contentTypeKey.ToString(),
                    Name = "New Page",
                    Culture = "en-US",
                },
                Guid.NewGuid()),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        created.Verify(x => x.SetCultureName("New Page", "en-US"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_InvariantContentType_DoesNotSetACultureName()
    {
        var parentKey = Guid.NewGuid();
        var created = SetupInvariantCreate(parentKey, out var contentTypeKey);

        var result = await _action.ExecuteAsync(
            CreateContext(
                new CreateContentSettings
                {
                    ParentKey = parentKey.ToString(),
                    ContentType = contentTypeKey.ToString(),
                    Name = "New Page",
                    Culture = "en-US",
                },
                Guid.NewGuid()),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        created.Verify(x => x.SetCultureName(It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NoBackofficeUserAndNoServiceAccount_Throws()
    {
        // The authorizer is mocked to succeed here, which is the only way to reach the identity
        // lookup: the real AutomationActionAuthorizer returns a Failed result with the same
        // "No backoffice identity available" message first when there is no current user. The
        // throw is therefore a last-line guard, shared by every built-in CMS write action, and
        // ErrorHandlingMiddleware turns it into a Failed step if it is ever reached in a run.
        var parentKey = Guid.NewGuid();
        SetupInvariantCreate(parentKey, out var contentTypeKey);

        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = parentKey.ToString(),
                ContentType = contentTypeKey.ToString(),
                Name = "New Page",
            },
            serviceAccountKey: null);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => _action.ExecuteAsync(context, CancellationToken.None));
        ex.Message.ShouldContain("No backoffice identity available");
        _contentService.Verify(
            x => x.Create(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
    }

    /// <summary>
    /// Sets up an invariant content type under an existing parent whose created item carries a
    /// property for each of <paramref name="propertyAliases"/>, and a successful save.
    /// </summary>
    private Mock<IContent> SetupInvariantCreate(Guid parentKey, out Guid contentTypeKey, params string[] propertyAliases)
    {
        var typeKey = Guid.NewGuid();
        contentTypeKey = typeKey;
        _contentService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IContent>());

        var contentType = new Mock<IContentType>();
        contentType.SetupGet(x => x.Alias).Returns("page");
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        _contentTypeService.Setup(x => x.Get(typeKey)).Returns(contentType.Object);

        var properties = new PropertyCollection();
        foreach (var alias in propertyAliases)
        {
            var propertyType = new Mock<IPropertyType>();
            propertyType.SetupGet(x => x.Alias).Returns(alias);
            properties.Add(new Property(propertyType.Object));
        }

        var created = new Mock<IContent>();
        created.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        created.SetupGet(x => x.Properties).Returns(properties);
        // The skipped-alias log entry names the content type.
        created.SetupGet(x => x.ContentType).Returns(Mock.Of<ISimpleContentType>(t => t.Alias == "page"));

        _contentService.Setup(x => x.Create("New Page", parentKey, "page", -1)).Returns(created.Object);
        _contentService
            .Setup(x => x.Save(created.Object, It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()))
            .Returns(new OperationResult(OperationResultType.Success, new EventMessages()));

        return created;
    }

    private static ActionContext CreateContext(CreateContentSettings settings, Guid? serviceAccountKey = null)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.createContent",
            Settings = settings,
            ExecutionContext = serviceAccountKey.HasValue
                ? new AutomationExecutionContext
                {
                    ServiceAccountKey = serviceAccountKey.Value,
                    WorkspaceId = Guid.NewGuid(),
                    WorkspaceName = "Test",
                    AutomationId = Guid.NewGuid(),
                    AutomationName = "Test",
                    RunId = Guid.NewGuid(),
                    InitiatorType = "test",
                    AllowedConnections = [],
                }
                : null,
        };
}
