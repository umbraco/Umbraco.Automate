using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class GetContentPropertyActionTests
{
    private readonly Mock<IPublishedContentCache> _cache = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IContentValueNormaliser> _normaliser = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();
    private readonly GetContentPropertyAction _action;

    public GetContentPropertyActionTests()
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

        _action = new GetContentPropertyAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _cache.Object,
            _contextFactory.Object,
            _normaliser.Object,
            _authorizer.Object,
            Mock.Of<ILogger<GetContentPropertyAction>>());
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.getContentProperty");

    [Fact]
    public async Task ExecuteAsync_InvalidContentKey_ReturnsValidationError()
    {
        var context = CreateContext(new GetContentPropertySettings
        {
            ContentKey = "not-a-guid",
            PropertyAlias = "title",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyPropertyAlias_ReturnsValidationError()
    {
        var context = CreateContext(new GetContentPropertySettings
        {
            ContentKey = Guid.NewGuid().ToString(),
            PropertyAlias = "",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ContentNotFound_ReturnsNotFoundOutcome()
    {
        var contentKey = Guid.NewGuid();
        _cache.Setup(x => x.GetByIdAsync(contentKey, It.IsAny<bool?>()))
            .ReturnsAsync((IPublishedContent?)null);

        var context = CreateContext(new GetContentPropertySettings
        {
            ContentKey = contentKey.ToString(),
            PropertyAlias = "title",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(GetContentPropertyAction.OutcomeNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_PropertyNotFound_ReturnsPropertyNotFoundOutcome()
    {
        var contentKey = Guid.NewGuid();
        var content = MockPublishedContent(contentKey);
        // GetProperty returns null for any alias by default — that's the "not found" case.

        _cache.Setup(x => x.GetByIdAsync(contentKey, It.IsAny<bool?>()))
            .ReturnsAsync(content.Object);

        var context = CreateContext(new GetContentPropertySettings
        {
            ContentKey = contentKey.ToString(),
            PropertyAlias = "doesNotExist",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(GetContentPropertyAction.OutcomePropertyNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_PropertyFound_ReturnsValue()
    {
        var contentKey = Guid.NewGuid();
        var content = MockPublishedContent(contentKey);

        var prop = new Mock<IPublishedProperty>();
        prop.SetupGet(x => x.Alias).Returns("title");
        content.Setup(x => x.GetProperty("title")).Returns(prop.Object);

        _cache.Setup(x => x.GetByIdAsync(contentKey, It.IsAny<bool?>()))
            .ReturnsAsync(content.Object);

        _normaliser.Setup(x => x.ReadProperty(content.Object, "title", null))
            .Returns("Hello");

        var context = CreateContext(new GetContentPropertySettings
        {
            ContentKey = contentKey.ToString(),
            PropertyAlias = "title",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();

        var output = result.OutputData as GetContentPropertyOutput;
        output.ShouldNotBeNull();
        output.Value.ShouldBe("Hello");
        output.HasValue.ShouldBeTrue();
    }

    private static Mock<IPublishedContent> MockPublishedContent(Guid key)
    {
        var contentType = new Mock<IPublishedContentType>();
        contentType.SetupGet(x => x.Alias).Returns("page");
        contentType.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);

        var content = new Mock<IPublishedContent>();
        content.SetupGet(x => x.Key).Returns(key);
        content.SetupGet(x => x.Name).Returns("Test");
        content.SetupGet(x => x.ContentType).Returns(contentType.Object);
        content.SetupGet(x => x.Cultures).Returns(new Dictionary<string, PublishedCultureInfo>(StringComparer.OrdinalIgnoreCase));
        content.SetupGet(x => x.Properties).Returns(Array.Empty<IPublishedProperty>());
        return content;
    }

    private static ActionContext CreateContext(GetContentPropertySettings settings)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.getContentProperty",
            Settings = settings,
        };
}
