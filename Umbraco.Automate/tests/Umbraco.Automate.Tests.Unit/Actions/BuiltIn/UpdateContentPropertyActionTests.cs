using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class UpdateContentPropertyActionTests
{
    private readonly Mock<IContentService> _contentService = new();
    private readonly Mock<IUserIdKeyResolver> _userIdKeyResolver = new();
    private readonly Mock<IBackOfficeSecurityAccessor> _securityAccessor = new();
    private readonly UpdateContentPropertyAction _action;

    public UpdateContentPropertyActionTests()
    {
        _userIdKeyResolver.Setup(x => x.GetAsync(It.IsAny<Guid>())).ReturnsAsync(-1);

        _action = new UpdateContentPropertyAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _contentService.Object,
            _userIdKeyResolver.Object,
            _securityAccessor.Object,
            Mock.Of<ILogger<UpdateContentPropertyAction>>());
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.updateContentProperty");

    [Fact]
    public async Task ExecuteAsync_InvalidContentKey_ReturnsValidationError()
    {
        var context = CreateContext(new UpdateContentPropertySettings
        {
            ContentKey = "not-a-guid",
            PropertyAlias = "title",
            Value = "Hello",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyPropertyAlias_ReturnsValidationError()
    {
        var context = CreateContext(new UpdateContentPropertySettings
        {
            ContentKey = Guid.NewGuid().ToString(),
            PropertyAlias = "",
            Value = "Hello",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ContentNotFound_ReturnsNotFoundOutcome()
    {
        var contentKey = Guid.NewGuid();
        _contentService.Setup(x => x.GetById(contentKey)).Returns((IContent?)null);

        var context = CreateContext(
            new UpdateContentPropertySettings
            {
                ContentKey = contentKey.ToString(),
                PropertyAlias = "title",
                Value = "Hello",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(UpdateContentPropertyAction.OutcomeNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_PropertyNotFound_ReturnsPropertyNotFoundOutcome()
    {
        var contentKey = Guid.NewGuid();
        var content = MockContent(contentKey, propertyAliases: []);
        _contentService.Setup(x => x.GetById(contentKey)).Returns(content.Object);

        var context = CreateContext(
            new UpdateContentPropertySettings
            {
                ContentKey = contentKey.ToString(),
                PropertyAlias = "doesNotExist",
                Value = "Hello",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(UpdateContentPropertyAction.OutcomePropertyNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_PropertyExists_WritesValueAndSaves()
    {
        var contentKey = Guid.NewGuid();
        var content = MockContent(contentKey, propertyAliases: ["title"]);
        content.Setup(x => x.GetValue("title", null, null, false)).Returns("Previous");

        _contentService.Setup(x => x.GetById(contentKey)).Returns(content.Object);
        _contentService
            .Setup(x => x.Save(content.Object, It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()))
            .Returns(new OperationResult(OperationResultType.Success, new EventMessages()));

        var context = CreateContext(
            new UpdateContentPropertySettings
            {
                ContentKey = contentKey.ToString(),
                PropertyAlias = "title",
                Value = "Updated",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();

        content.Verify(x => x.SetValue("title", "Updated", null, null), Times.Once);

        var output = result.OutputData as UpdateContentPropertyOutput;
        output.ShouldNotBeNull();
        output.ContentKey.ShouldBe(contentKey);
        output.PropertyAlias.ShouldBe("title");
        output.PreviousValue.ShouldBe("Previous");
    }

    [Fact]
    public async Task ExecuteAsync_SaveCancelledByEvent_MapsToCancelled()
    {
        var contentKey = Guid.NewGuid();
        var content = MockContent(contentKey, propertyAliases: ["title"]);

        _contentService.Setup(x => x.GetById(contentKey)).Returns(content.Object);
        _contentService
            .Setup(x => x.Save(content.Object, It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()))
            .Returns(new OperationResult(OperationResultType.FailedCancelledByEvent, new EventMessages()));

        var context = CreateContext(
            new UpdateContentPropertySettings
            {
                ContentKey = contentKey.ToString(),
                PropertyAlias = "title",
                Value = "Updated",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Cancelled);
    }

    private static Mock<IContent> MockContent(Guid key, string[] propertyAliases)
    {
        var contentType = new Mock<ISimpleContentType>();
        contentType.SetupGet(x => x.Alias).Returns("page");
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);

        var properties = new PropertyCollection(
            propertyAliases.Select(alias =>
            {
                var propertyType = new Mock<IPropertyType>();
                propertyType.SetupGet(x => x.Alias).Returns(alias);
                propertyType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
                return new Property(propertyType.Object);
            }));

        var content = new Mock<IContent>();
        content.SetupGet(x => x.Key).Returns(key);
        content.SetupGet(x => x.ContentType).Returns(contentType.Object);
        content.SetupGet(x => x.Properties).Returns(properties);
        content.SetupGet(x => x.AvailableCultures).Returns([]);
        return content;
    }

    private static ActionContext CreateContext(UpdateContentPropertySettings settings, Guid? serviceAccountKey = null)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.updateContentProperty",
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
