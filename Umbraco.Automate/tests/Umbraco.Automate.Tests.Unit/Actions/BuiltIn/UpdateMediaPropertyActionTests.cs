using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class UpdateMediaPropertyActionTests
{
    private readonly Mock<IMediaService> _mediaService = new();
    private readonly Mock<IUserIdKeyResolver> _userIdKeyResolver = new();
    private readonly Mock<IBackOfficeSecurityAccessor> _securityAccessor = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly UpdateMediaPropertyAction _action;

    public UpdateMediaPropertyActionTests()
    {
        _userIdKeyResolver.Setup(x => x.GetAsync(It.IsAny<Guid>())).ReturnsAsync(-1);

        _contextFactory
            .Setup(x => x.EnsureUmbracoContext())
            .Returns(new UmbracoContextReference(
                Mock.Of<IUmbracoContext>(),
                isRoot: false,
                Mock.Of<IUmbracoContextAccessor>()));

        _action = new UpdateMediaPropertyAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _mediaService.Object,
            _userIdKeyResolver.Object,
            _securityAccessor.Object,
            _contextFactory.Object,
            Mock.Of<ILogger<UpdateMediaPropertyAction>>());
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.updateMediaProperty");

    [Fact]
    public async Task ExecuteAsync_InvalidMediaKey_ReturnsValidationError()
    {
        var context = CreateContext(new UpdateMediaPropertySettings
        {
            MediaKey = "not-a-guid",
            PropertyAlias = "alt",
            Value = "Sunset over the harbour",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyPropertyAlias_ReturnsValidationError()
    {
        var context = CreateContext(new UpdateMediaPropertySettings
        {
            MediaKey = Guid.NewGuid().ToString(),
            PropertyAlias = "",
            Value = "Sunset over the harbour",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_MediaNotFound_ReturnsNotFoundOutcome()
    {
        var mediaKey = Guid.NewGuid();
        _mediaService.Setup(x => x.GetById(mediaKey)).Returns((IMedia?)null);

        var context = CreateContext(
            new UpdateMediaPropertySettings
            {
                MediaKey = mediaKey.ToString(),
                PropertyAlias = "alt",
                Value = "Sunset over the harbour",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(UpdateMediaPropertyAction.OutcomeNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_PropertyNotFound_ReturnsPropertyNotFoundOutcome()
    {
        var mediaKey = Guid.NewGuid();
        var media = MockMedia(mediaKey, propertyAliases: []);
        _mediaService.Setup(x => x.GetById(mediaKey)).Returns(media.Object);

        var context = CreateContext(
            new UpdateMediaPropertySettings
            {
                MediaKey = mediaKey.ToString(),
                PropertyAlias = "doesNotExist",
                Value = "Sunset over the harbour",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(UpdateMediaPropertyAction.OutcomePropertyNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_PropertyExists_WritesValueAndSaves()
    {
        var mediaKey = Guid.NewGuid();
        var media = MockMedia(mediaKey, propertyAliases: ["alt"]);
        media.Setup(x => x.GetValue("alt", null, null, false)).Returns("Old alt");

        _mediaService.Setup(x => x.GetById(mediaKey)).Returns(media.Object);
        _mediaService
            .Setup(x => x.Save(media.Object, It.IsAny<int>()))
            .Returns(Attempt<OperationResult?>.Succeed(new OperationResult(OperationResultType.Success, new EventMessages())));

        var context = CreateContext(
            new UpdateMediaPropertySettings
            {
                MediaKey = mediaKey.ToString(),
                PropertyAlias = "alt",
                Value = "Sunset over the harbour",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();

        media.Verify(x => x.SetValue("alt", "Sunset over the harbour", null, null), Times.Once);

        var output = result.OutputData as UpdateMediaPropertyOutput;
        output.ShouldNotBeNull();
        output.MediaKey.ShouldBe(mediaKey);
        output.PropertyAlias.ShouldBe("alt");
        output.PreviousValue.ShouldBe("Old alt");
    }

    [Fact]
    public async Task ExecuteAsync_SaveCancelledByEvent_MapsToCancelled()
    {
        var mediaKey = Guid.NewGuid();
        var media = MockMedia(mediaKey, propertyAliases: ["alt"]);

        _mediaService.Setup(x => x.GetById(mediaKey)).Returns(media.Object);
        _mediaService
            .Setup(x => x.Save(media.Object, It.IsAny<int>()))
            .Returns(Attempt<OperationResult?>.Fail(new OperationResult(OperationResultType.FailedCancelledByEvent, new EventMessages())));

        var context = CreateContext(
            new UpdateMediaPropertySettings
            {
                MediaKey = mediaKey.ToString(),
                PropertyAlias = "alt",
                Value = "Sunset over the harbour",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Cancelled);
    }

    private static Mock<IMedia> MockMedia(Guid key, string[] propertyAliases)
    {
        var contentType = new Mock<ISimpleContentType>();
        contentType.SetupGet(x => x.Alias).Returns("Image");
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);

        var properties = new PropertyCollection(
            propertyAliases.Select(alias =>
            {
                var propertyType = new Mock<IPropertyType>();
                propertyType.SetupGet(x => x.Alias).Returns(alias);
                propertyType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
                return new Property(propertyType.Object);
            }));

        var media = new Mock<IMedia>();
        media.SetupGet(x => x.Key).Returns(key);
        media.SetupGet(x => x.ContentType).Returns(contentType.Object);
        media.SetupGet(x => x.Properties).Returns(properties);
        media.SetupGet(x => x.AvailableCultures).Returns([]);
        return media;
    }

    private static ActionContext CreateContext(UpdateMediaPropertySettings settings, Guid? serviceAccountKey = null)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.updateMediaProperty",
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
