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

public class MoveMediaActionTests
{
    private readonly Mock<IMediaEditingService> _mediaEditingService = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();
    private readonly MoveMediaAction _action;

    public MoveMediaActionTests()
    {
        _contextFactory
            .Setup(x => x.EnsureUmbracoContext())
            .Returns(new UmbracoContextReference(
                Mock.Of<IUmbracoContext>(),
                isRoot: false,
                Mock.Of<IUmbracoContextAccessor>()));

        _authorizer
            .Setup(a => a.AuthorizeMediaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _action = new MoveMediaAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _mediaEditingService.Object,
            Mock.Of<IBackOfficeSecurityAccessor>(),
            _contextFactory.Object,
            _authorizer.Object,
            Mock.Of<ILogger<MoveMediaAction>>());
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.moveMedia");

    [Fact]
    public void HasCorrectName()
        => _action.Name.ShouldBe("Move Media");

    [Fact]
    public void HasSettingsType()
        => _action.SettingsType.ShouldBe(typeof(MoveMediaSettings));

    [Fact]
    public void RequiresNoPermissionLetters()
        => _action.RequiredPermissions.ShouldBeEmpty();

    [Fact]
    public void RequiresMediaSection()
        => _action.RequiredSections.ShouldContain(Constants.Applications.Media);

    [Fact]
    public async Task ExecuteAsync_InvalidMediaKey_ReturnsValidationError()
    {
        var context = CreateContext(new MoveMediaSettings { MediaKey = "not-a-guid" });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyMediaKey_ReturnsValidationError()
    {
        var context = CreateContext(new MoveMediaSettings { MediaKey = "" });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidTargetParentKey_ReturnsValidationError()
    {
        var context = CreateContext(new MoveMediaSettings
        {
            MediaKey = Guid.NewGuid().ToString(),
            TargetParentKey = "not-a-guid",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulMoveUnderParent_ReturnsSuccessWithNewParentKey()
    {
        var mediaKey = Guid.NewGuid();
        var targetParentKey = Guid.NewGuid();

        _mediaEditingService
            .Setup(x => x.MoveAsync(mediaKey, targetParentKey, It.IsAny<Guid>()))
            .ReturnsAsync(Attempt<IMedia?, ContentEditingOperationStatus>.Succeed(
                ContentEditingOperationStatus.Success, Mock.Of<IMedia>()));

        var context = CreateContext(
            new MoveMediaSettings { MediaKey = mediaKey.ToString(), TargetParentKey = targetParentKey.ToString() },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        var output = result.OutputData.ShouldBeOfType<MoveMediaOutput>();
        output.MediaKey.ShouldBe(mediaKey);
        output.ParentKey.ShouldBe(targetParentKey);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulMoveToRoot_ReturnsSuccessWithNullParentKey()
    {
        var mediaKey = Guid.NewGuid();

        _mediaEditingService
            .Setup(x => x.MoveAsync(mediaKey, null, It.IsAny<Guid>()))
            .ReturnsAsync(Attempt<IMedia?, ContentEditingOperationStatus>.Succeed(
                ContentEditingOperationStatus.Success, Mock.Of<IMedia>()));

        var context = CreateContext(
            new MoveMediaSettings { MediaKey = mediaKey.ToString(), TargetParentKey = "" },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        var output = result.OutputData.ShouldBeOfType<MoveMediaOutput>();
        output.ParentKey.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_MediaNotFound_ReturnsValidationError()
    {
        var mediaKey = Guid.NewGuid();

        _mediaEditingService
            .Setup(x => x.MoveAsync(mediaKey, null, It.IsAny<Guid>()))
            .ReturnsAsync(Attempt<IMedia?, ContentEditingOperationStatus>.Fail(ContentEditingOperationStatus.NotFound));

        var context = CreateContext(
            new MoveMediaSettings { MediaKey = mediaKey.ToString() },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_Unauthorised_ReturnsAuthenticationErrorNotCrash()
    {
        var mediaKey = Guid.NewGuid();

        _authorizer
            .Setup(a => a.AuthorizeMediaAsync(mediaKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("no access"));

        var context = CreateContext(
            new MoveMediaSettings { MediaKey = mediaKey.ToString() },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
        _mediaEditingService.Verify(
            x => x.MoveAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid>()),
            Times.Never);
    }

    private static ActionContext CreateContext(MoveMediaSettings settings, Guid? serviceAccountKey = null)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.moveMedia",
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
