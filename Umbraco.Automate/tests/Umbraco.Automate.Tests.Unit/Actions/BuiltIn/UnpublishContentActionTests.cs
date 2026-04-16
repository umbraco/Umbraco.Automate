using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class UnpublishContentActionTests
{
    private readonly Mock<IContentPublishingService> _publishingService = new();
    private readonly UnpublishContentAction _action;

    public UnpublishContentActionTests()
    {
        _action = new UnpublishContentAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _publishingService.Object,
            Mock.Of<IBackOfficeSecurityAccessor>(),
            Mock.Of<ILogger<UnpublishContentAction>>());
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.unpublishContent");

    [Fact]
    public void HasCorrectName()
        => _action.Name.ShouldBe("Unpublish Content");

    [Fact]
    public void HasSettingsType()
        => _action.SettingsType.ShouldBe(typeof(UnpublishContentActionSettings));

    [Fact]
    public async Task ExecuteAsync_InvalidContentKey_ReturnsValidationError()
    {
        var context = CreateContext(new UnpublishContentActionSettings { ContentKey = "not-a-guid" });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyContentKey_ReturnsValidationError()
    {
        var context = CreateContext(new UnpublishContentActionSettings { ContentKey = "" });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulUnpublish_ReturnsSuccess()
    {
        var contentKey = Guid.NewGuid();

        _publishingService
            .Setup(x => x.UnpublishAsync(contentKey, null, It.IsAny<Guid>()))
            .ReturnsAsync(Attempt<ContentPublishingOperationStatus>.Succeed(ContentPublishingOperationStatus.Success));

        var context = CreateContext(
            new UnpublishContentActionSettings { ContentKey = contentKey.ToString() },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.OutputData.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithCultures_ParsesCulturesCorrectly()
    {
        var contentKey = Guid.NewGuid();

        _publishingService
            .Setup(x => x.UnpublishAsync(contentKey, It.Is<ISet<string>>(s => s.Contains("en-US") && s.Contains("da-DK")), It.IsAny<Guid>()))
            .ReturnsAsync(Attempt<ContentPublishingOperationStatus>.Succeed(ContentPublishingOperationStatus.Success));

        var context = CreateContext(
            new UnpublishContentActionSettings { ContentKey = contentKey.ToString(), Cultures = "en-US,da-DK" },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        _publishingService.Verify(x => x.UnpublishAsync(contentKey, It.IsAny<ISet<string>>(), It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ContentNotFound_ReturnsValidationError()
    {
        var contentKey = Guid.NewGuid();

        _publishingService
            .Setup(x => x.UnpublishAsync(contentKey, null, It.IsAny<Guid>()))
            .ReturnsAsync(Attempt<ContentPublishingOperationStatus>.Fail(ContentPublishingOperationStatus.ContentNotFound));

        var context = CreateContext(
            new UnpublishContentActionSettings { ContentKey = contentKey.ToString() },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    private static ActionContext CreateContext(UnpublishContentActionSettings settings, Guid? serviceAccountKey = null)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.unpublishContent",
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
