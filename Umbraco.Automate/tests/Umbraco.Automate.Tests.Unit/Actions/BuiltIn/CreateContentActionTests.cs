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
    public async Task ExecuteAsync_EmptyContentTypeAlias_ReturnsValidationError()
    {
        var context = CreateContext(new CreateContentSettings
        {
            ParentKey = Guid.NewGuid().ToString(),
            ContentTypeAlias = "",
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
            ContentTypeAlias = "page",
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
            ContentTypeAlias = "page",
            Name = "New Page",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ParentNotFound_ReturnsParentNotFoundOutcome()
    {
        var parentKey = Guid.NewGuid();
        _contentService.Setup(x => x.GetById(parentKey)).Returns((IContent?)null);

        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = parentKey.ToString(),
                ContentTypeAlias = "page",
                Name = "New Page",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(CreateContentAction.OutcomeParentNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ContentTypeNotFound_ReturnsContentTypeNotFoundOutcome()
    {
        var parentKey = Guid.NewGuid();
        _contentService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IContent>());
        _contentTypeService.Setup(x => x.Get("doesNotExist")).Returns((IContentType?)null);

        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = parentKey.ToString(),
                ContentTypeAlias = "doesNotExist",
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
        _contentService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IContent>());

        var contentType = new Mock<IContentType>();
        contentType.SetupGet(x => x.Alias).Returns("page");
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Culture);
        _contentTypeService.Setup(x => x.Get("page")).Returns(contentType.Object);

        var context = CreateContext(
            new CreateContentSettings
            {
                ParentKey = parentKey.ToString(),
                ContentTypeAlias = "page",
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
        var contentKey = Guid.NewGuid();
        _contentService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IContent>());

        var contentType = new Mock<IContentType>();
        contentType.SetupGet(x => x.Alias).Returns("page");
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        _contentTypeService.Setup(x => x.Get("page")).Returns(contentType.Object);

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
                ContentTypeAlias = "page",
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
        output.ContentTypeAlias.ShouldBe("page");
        output.ParentKey.ShouldBe(parentKey);
    }

    [Fact]
    public async Task ExecuteAsync_SaveCancelledByEvent_MapsToCancelled()
    {
        var parentKey = Guid.NewGuid();
        _contentService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IContent>());

        var contentType = new Mock<IContentType>();
        contentType.SetupGet(x => x.Alias).Returns("page");
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        _contentTypeService.Setup(x => x.Get("page")).Returns(contentType.Object);

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
                ContentTypeAlias = "page",
                Name = "New Page",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Cancelled);
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
