// S9 — Content and media actions offer named exits (docs/plans/action-outcomes/STORIES.md)
//
// Get Content is live (T8) and the other content actions are live (T18). The media actions stay pending
// until T19; move each InlineData row out of the skipped theories as its task lands.

using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Examine;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Realtime;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class BuiltInActionOutcomeTests
{
    #region Given each built-in action that returns outcomes today

    [Theory(Skip = "Pending: T19")]
    [InlineData("GetMediaAction")]
    [InlineData("GetMediaPropertyAction")]
    [InlineData("FindMediaAction")]
    [InlineData("CreateMediaAction")]
    [InlineData("UpdateMediaPropertyAction")]
    public void GetOutcomes_BuiltInAction_DefaultIsSuccess(string actionType)
    {
        // Then the single default outcome's key is "success".
        _ = actionType;
    }

    [Theory(Skip = "Pending: T19")]
    [InlineData("GetMediaAction")]
    [InlineData("GetMediaPropertyAction")]
    [InlineData("FindMediaAction")]
    [InlineData("CreateMediaAction")]
    [InlineData("UpdateMediaPropertyAction")]
    public void GetOutcomes_BuiltInAction_KeysAreSuccessThenExistingConstants(string actionType)
    {
        // Then the keys equal ["success", ...the action's public Outcome* constant values].
        _ = actionType;
    }

    [Theory(Skip = "Pending: T19")]
    [InlineData("GetMediaAction")]
    [InlineData("GetMediaPropertyAction")]
    [InlineData("FindMediaAction")]
    [InlineData("CreateMediaAction")]
    [InlineData("UpdateMediaPropertyAction")]
    public void GetOutcomes_BuiltInAction_AllLabelsAreLocalizationKeys(string actionType)
    {
        // Then every label starts with "#uaOutcomes_".
        _ = actionType;
    }

    #endregion

    #region Given each content action that returns outcomes

    // Expected keys are literal on purpose: they are what saved automations store, so a renamed
    // constant must fail here rather than quietly follow the rename.
    public static TheoryData<string, string[]> ContentActionOutcomeKeys => new()
    {
        { "GetContentPropertyAction", ["success", "notFound", "propertyNotFound"] },
        { "FindContentAction", ["success", "notFound"] },
        { "CreateContentAction", ["success", "parentNotFound", "contentTypeNotFound"] },
        { "UpdateContentPropertyAction", ["success", "notFound", "propertyNotFound"] },
        { "NotifyEditorAction", ["success", "notFound"] },
    };

    [Theory]
    [MemberData(nameof(ContentActionOutcomeKeys))]
    public void GetOutcomes_ContentAction_DefaultIsSuccess(string actionType, string[] expectedKeys)
    {
        _ = expectedKeys;

        CreateContentAction(actionType).GetOutcomes().Single(o => o.IsDefault).Key.ShouldBe("success");
    }

    [Theory]
    [MemberData(nameof(ContentActionOutcomeKeys))]
    public void GetOutcomes_ContentAction_KeysAreSuccessThenExistingConstants(string actionType, string[] expectedKeys)
        => CreateContentAction(actionType).GetOutcomes().Select(o => o.Key).ShouldBe(expectedKeys);

    [Theory]
    [MemberData(nameof(ContentActionOutcomeKeys))]
    public void GetOutcomes_ContentAction_AllLabelsAreLocalizationKeys(string actionType, string[] expectedKeys)
    {
        _ = expectedKeys;

        CreateContentAction(actionType).GetOutcomes().ShouldAllBe(o => o.Label.StartsWith("#uaOutcomes_"));
    }

    #endregion

    #region Given Get Content

    [Fact]
    public void GetOutcomes_GetContent_DefaultIsSuccess()
        => CreateGetContentAction().GetOutcomes().Single(o => o.IsDefault).Key.ShouldBe("success");

    [Fact]
    public void GetOutcomes_GetContent_KeysAreSuccessThenNotFound()
        => CreateGetContentAction().GetOutcomes().Select(o => o.Key)
            .ShouldBe([GetContentAction.OutcomeSuccess, GetContentAction.OutcomeNotFound]);

    [Fact]
    public void GetOutcomes_GetContent_AllLabelsAreLocalizationKeys()
        => CreateGetContentAction().GetOutcomes().ShouldAllBe(o => o.Label.StartsWith("#uaOutcomes_"));

    // Not asserted here: that each "#uaOutcomes_<key>" label has a term in lang/en.ts. A C# test
    // cannot read the TypeScript dictionary; the frontend build and manual check cover it.

    #endregion

    #region Given Get Content and missing content

    [Fact]
    public async Task Execute_GetContentMissing_ReturnsNotFoundKeyUnchanged()
    {
        var cache = new Mock<IPublishedContentCache>();
        cache.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool?>()))
            .ReturnsAsync((IPublishedContent?)null);
        var authorizer = new Mock<IAutomationActionAuthorizer>();
        authorizer
            .Setup(a => a.AuthorizeContentAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);
        var action = CreateGetContentAction(cache.Object, authorizer.Object);
        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.getContent",
            Settings = new GetContentSettings { ContentKey = Guid.NewGuid().ToString() },
        };

        var result = await action.ExecuteAsync(context, CancellationToken.None);

        result.Outcome.ShouldBe(GetContentAction.OutcomeNotFound);
    }

    #endregion

    private static IAction CreateContentAction(string actionType)
    {
        var infrastructure = new ActionInfrastructure(Mock.Of<IEditableModelResolver>());

        return actionType switch
        {
            "GetContentPropertyAction" => new GetContentPropertyAction(
                infrastructure,
                Mock.Of<IPublishedContentCache>(),
                Mock.Of<IUmbracoContextFactory>(),
                Mock.Of<IContentValueNormaliser>(),
                Mock.Of<IAutomationActionAuthorizer>(),
                Mock.Of<IVariationContextAccessor>(),
                Mock.Of<ILogger<GetContentPropertyAction>>()),
            "FindContentAction" => new FindContentAction(
                infrastructure,
                Mock.Of<IExamineManager>(),
                Mock.Of<IContentTypeService>(),
                Mock.Of<IPublishedContentCache>(),
                Mock.Of<IUmbracoContextFactory>(),
                Mock.Of<IPublishedUrlProvider>(),
                Mock.Of<IAutomationActionAuthorizer>(),
                Mock.Of<ILogger<FindContentAction>>()),
            "CreateContentAction" => new CreateContentAction(
                infrastructure,
                Mock.Of<IContentService>(),
                Mock.Of<IContentTypeService>(),
                Mock.Of<IUserIdKeyResolver>(),
                Mock.Of<IBackOfficeSecurityAccessor>(),
                Mock.Of<IUmbracoContextFactory>(),
                Mock.Of<IAutomationActionAuthorizer>(),
                Mock.Of<ILogger<CreateContentAction>>()),
            "UpdateContentPropertyAction" => new UpdateContentPropertyAction(
                infrastructure,
                Mock.Of<IContentService>(),
                Mock.Of<IUserIdKeyResolver>(),
                Mock.Of<IBackOfficeSecurityAccessor>(),
                Mock.Of<IUmbracoContextFactory>(),
                Mock.Of<IAutomationActionAuthorizer>(),
                Mock.Of<ILogger<UpdateContentPropertyAction>>()),
            "NotifyEditorAction" => new NotifyEditorAction(
                infrastructure,
                Mock.Of<IContentService>(),
                Mock.Of<IAutomationService>(),
                Mock.Of<IEditorNotifier>(),
                Mock.Of<ILogger<NotifyEditorAction>>()),
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null),
        };
    }

    private static GetContentAction CreateGetContentAction(
        IPublishedContentCache? cache = null,
        IAutomationActionAuthorizer? authorizer = null)
        => new(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            cache ?? Mock.Of<IPublishedContentCache>(),
            Mock.Of<IUmbracoContextFactory>(f => f.EnsureUmbracoContext() == new UmbracoContextReference(
                Mock.Of<IUmbracoContext>(),
                false,
                Mock.Of<IUmbracoContextAccessor>())),
            Mock.Of<IPublishedUrlProvider>(),
            Mock.Of<IUserIdKeyResolver>(),
            Mock.Of<IContentValueNormaliser>(),
            authorizer ?? Mock.Of<IAutomationActionAuthorizer>(),
            Mock.Of<IDocumentNavigationQueryService>(),
            Mock.Of<IVariationContextAccessor>(),
            Mock.Of<ILogger<GetContentAction>>());
}
