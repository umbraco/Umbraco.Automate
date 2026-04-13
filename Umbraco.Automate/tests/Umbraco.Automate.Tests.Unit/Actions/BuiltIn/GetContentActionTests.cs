using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class GetContentActionTests
{
    private readonly Mock<IPublishedContentCache> _cache = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IPublishedUrlProvider> _urlProvider = new();
    private readonly Mock<IContentValueNormaliser> _normaliser = new();
    private readonly GetContentAction _action;

    public GetContentActionTests()
    {
        _contextFactory
            .Setup(x => x.EnsureUmbracoContext())
            .Returns(new UmbracoContextReference(
                Mock.Of<IUmbracoContext>(),
                isRoot: false,
                Mock.Of<IUmbracoContextAccessor>()));

        _action = new GetContentAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _cache.Object,
            _contextFactory.Object,
            _urlProvider.Object,
            _normaliser.Object,
            Mock.Of<ILogger<GetContentAction>>());
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.getContent");

    [Fact]
    public void HasCorrectName()
        => _action.Name.ShouldBe("Get Content");

    [Fact]
    public async Task ExecuteAsync_InvalidContentKey_ReturnsValidationError()
    {
        var context = CreateContext(new GetContentSettings { ContentKey = "not-a-guid" });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyContentKey_ReturnsValidationError()
    {
        var context = CreateContext(new GetContentSettings { ContentKey = "" });

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

        var context = CreateContext(new GetContentSettings { ContentKey = contentKey.ToString() });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(GetContentAction.OutcomeNotFound);

        var output = result.OutputData as GetContentOutput;
        output.ShouldNotBeNull();
        output.ContentKey.ShouldBe(contentKey);
    }

    [Fact]
    public async Task ExecuteAsync_ContentFound_ReturnsProjectedOutput()
    {
        var contentKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        var content = MockPublishedContent(contentKey, "About", "page", contentTypeKey);

        _cache.Setup(x => x.GetByIdAsync(contentKey, It.IsAny<bool?>()))
            .ReturnsAsync(content.Object);

        var properties = new Dictionary<string, object?> { ["title"] = "Hello" };
        _normaliser.Setup(x => x.NormaliseProperties(content.Object, null))
            .Returns(properties);

        var context = CreateContext(new GetContentSettings { ContentKey = contentKey.ToString() });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();

        var output = result.OutputData as GetContentOutput;
        output.ShouldNotBeNull();
        output.ContentKey.ShouldBe(contentKey);
        output.Name.ShouldBe("About");
        output.ContentTypeAlias.ShouldBe("page");
        output.ContentTypeKey.ShouldBe(contentTypeKey);
        output.Properties.ShouldContainKey("title");
        output.Properties["title"].ShouldBe("Hello");
    }

    private static Mock<IPublishedContent> MockPublishedContent(
        Guid key,
        string name,
        string typeAlias,
        Guid contentTypeKey)
    {
        var contentType = new Mock<IPublishedContentType>();
        contentType.SetupGet(x => x.Alias).Returns(typeAlias);
        contentType.SetupGet(x => x.Key).Returns(contentTypeKey);
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        contentType.SetupGet(x => x.ItemType).Returns(PublishedItemType.Content);

        var content = new Mock<IPublishedContent>();
        content.SetupGet(x => x.Key).Returns(key);
        content.SetupGet(x => x.Name).Returns(name);
        content.SetupGet(x => x.ContentType).Returns(contentType.Object);
        content.SetupGet(x => x.Cultures).Returns(new Dictionary<string, PublishedCultureInfo>(StringComparer.OrdinalIgnoreCase));
        content.SetupGet(x => x.Properties).Returns(Array.Empty<IPublishedProperty>());
        content.SetupGet(x => x.CreateDate).Returns(DateTime.UtcNow);
        content.SetupGet(x => x.UpdateDate).Returns(DateTime.UtcNow);
        return content;
    }

    private static ActionContext CreateContext(GetContentSettings settings)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.getContent",
            Settings = settings,
        };
}
