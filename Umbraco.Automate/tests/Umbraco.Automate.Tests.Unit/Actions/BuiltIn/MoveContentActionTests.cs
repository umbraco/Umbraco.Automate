using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class MoveContentActionTests
{
    private readonly Mock<IContentEditingService> _contentEditingService = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();
    private readonly MoveContentAction _action;

    public MoveContentActionTests()
    {
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
            .Setup(a => a.AuthorizeContentParentAsync(It.IsAny<Guid?>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _action = new MoveContentAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _contentEditingService.Object,
            Mock.Of<IBackOfficeSecurityAccessor>(),
            _contextFactory.Object,
            _authorizer.Object,
            Mock.Of<ILogger<MoveContentAction>>());
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.moveContent");

    [Fact]
    public void HasCorrectName()
        => _action.Name.ShouldBe("Move Content");

    [Fact]
    public void HasSettingsType()
        => _action.SettingsType.ShouldBe(typeof(MoveContentSettings));

    [Fact]
    public void RequiresMovePermission()
        => _action.RequiredPermissions.ShouldContain(ActionMoveLetter);

    [Fact]
    public async Task ExecuteAsync_InvalidContentKey_ReturnsValidationError()
    {
        var context = CreateContext(new MoveContentSettings { ContentKey = "not-a-guid" });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyContentKey_ReturnsValidationError()
    {
        var context = CreateContext(new MoveContentSettings { ContentKey = "" });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidTargetParentKey_ReturnsValidationError()
    {
        var context = CreateContext(new MoveContentSettings
        {
            ContentKey = Guid.NewGuid().ToString(),
            TargetParentKey = "not-a-guid",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulMoveUnderParent_ReturnsSuccessWithNewParentKey()
    {
        var contentKey = Guid.NewGuid();
        var targetParentKey = Guid.NewGuid();

        _contentEditingService
            .Setup(x => x.MoveAsync(contentKey, targetParentKey, It.IsAny<Guid>()))
            .ReturnsAsync(Attempt<IContent?, ContentEditingOperationStatus>.Succeed(
                ContentEditingOperationStatus.Success, Mock.Of<IContent>()));

        var context = CreateContext(
            new MoveContentSettings { ContentKey = contentKey.ToString(), TargetParentKey = targetParentKey.ToString() },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.OutputData.ShouldNotBeNull();
        var output = result.OutputData.ShouldBeOfType<MoveContentOutput>();
        output.ContentKey.ShouldBe(contentKey);
        output.ParentKey.ShouldBe(targetParentKey);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulMoveToRoot_ReturnsSuccessWithNullParentKey()
    {
        var contentKey = Guid.NewGuid();

        _contentEditingService
            .Setup(x => x.MoveAsync(contentKey, null, It.IsAny<Guid>()))
            .ReturnsAsync(Attempt<IContent?, ContentEditingOperationStatus>.Succeed(
                ContentEditingOperationStatus.Success, Mock.Of<IContent>()));

        var context = CreateContext(
            new MoveContentSettings { ContentKey = contentKey.ToString(), TargetParentKey = "" },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        var output = result.OutputData.ShouldBeOfType<MoveContentOutput>();
        output.ParentKey.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ContentNotFound_ReturnsValidationError()
    {
        var contentKey = Guid.NewGuid();

        _contentEditingService
            .Setup(x => x.MoveAsync(contentKey, null, It.IsAny<Guid>()))
            .ReturnsAsync(Attempt<IContent?, ContentEditingOperationStatus>.Fail(ContentEditingOperationStatus.NotFound));

        var context = CreateContext(
            new MoveContentSettings { ContentKey = contentKey.ToString() },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ParentNotFound_ReturnsValidationError()
    {
        var contentKey = Guid.NewGuid();
        var targetParentKey = Guid.NewGuid();

        _contentEditingService
            .Setup(x => x.MoveAsync(contentKey, targetParentKey, It.IsAny<Guid>()))
            .ReturnsAsync(Attempt<IContent?, ContentEditingOperationStatus>.Fail(ContentEditingOperationStatus.ParentNotFound));

        var context = CreateContext(
            new MoveContentSettings { ContentKey = contentKey.ToString(), TargetParentKey = targetParentKey.ToString() },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_Unauthorised_ReturnsAuthenticationErrorNotCrash()
    {
        var contentKey = Guid.NewGuid();

        _authorizer
            .Setup(a => a.AuthorizeContentAsync(contentKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("no access"));

        var context = CreateContext(
            new MoveContentSettings { ContentKey = contentKey.ToString() },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
        _contentEditingService.Verify(
            x => x.MoveAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_UnauthorisedTargetParent_ReturnsAuthenticationErrorNotCrash()
    {
        var contentKey = Guid.NewGuid();
        var targetParentKey = Guid.NewGuid();

        _authorizer
            .Setup(a => a.AuthorizeContentParentAsync(targetParentKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("no access to target parent"));

        var context = CreateContext(
            new MoveContentSettings { ContentKey = contentKey.ToString(), TargetParentKey = targetParentKey.ToString() },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
        _contentEditingService.Verify(
            x => x.MoveAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_UnauthorisedTargetRoot_ReturnsAuthenticationErrorNotCrash()
    {
        var contentKey = Guid.NewGuid();

        _authorizer
            .Setup(a => a.AuthorizeContentParentAsync(null, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("no access to root"));

        var context = CreateContext(
            new MoveContentSettings { ContentKey = contentKey.ToString(), TargetParentKey = "" },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
        _contentEditingService.Verify(
            x => x.MoveAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid>()),
            Times.Never);
    }

    private const string ActionMoveLetter = "Umb.Document.Move";

    private static ActionContext CreateContext(MoveContentSettings settings, Guid? serviceAccountKey = null)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.moveContent",
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
