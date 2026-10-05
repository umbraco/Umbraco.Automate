// S9 — Content and media actions offer named exits (docs/plans/action-outcomes/STORIES.md)
//
// Get Content is live (T8), the other content actions are live (T18) and the media actions are live (T19).

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
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class BuiltInActionOutcomeTests
{
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

    public static TheoryData<string, string[]> MediaActionOutcomeKeys => new()
    {
        { "GetMediaAction", ["success", "notFound"] },
        { "GetMediaPropertyAction", ["success", "notFound", "propertyNotFound"] },
        { "FindMediaAction", ["success", "notFound"] },
        { "CreateMediaAction", ["success", "parentNotFound", "mediaTypeNotFound", "fileDownloadFailed"] },
        { "UpdateMediaPropertyAction", ["success", "notFound", "propertyNotFound"] },
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

    #region Given each media action that returns outcomes

    [Theory]
    [MemberData(nameof(MediaActionOutcomeKeys))]
    public void GetOutcomes_MediaAction_DefaultIsSuccess(string actionType, string[] expectedKeys)
    {
        _ = expectedKeys;

        CreateMediaAction(actionType).GetOutcomes().Single(o => o.IsDefault).Key.ShouldBe("success");
    }

    [Theory]
    [MemberData(nameof(MediaActionOutcomeKeys))]
    public void GetOutcomes_MediaAction_KeysAreSuccessThenExistingConstants(string actionType, string[] expectedKeys)
        => CreateMediaAction(actionType).GetOutcomes().Select(o => o.Key).ShouldBe(expectedKeys);

    [Theory]
    [MemberData(nameof(MediaActionOutcomeKeys))]
    public void GetOutcomes_MediaAction_AllLabelsAreLocalizationKeys(string actionType, string[] expectedKeys)
    {
        _ = expectedKeys;

        CreateMediaAction(actionType).GetOutcomes().ShouldAllBe(o => o.Label.StartsWith("#uaOutcomes_"));
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

    private static IAction CreateMediaAction(string actionType)
    {
        var infrastructure = new ActionInfrastructure(Mock.Of<IEditableModelResolver>());

        return actionType switch
        {
            "GetMediaAction" => new GetMediaAction(
                infrastructure,
                Mock.Of<IPublishedMediaCache>(),
                Mock.Of<IUmbracoContextFactory>(),
                Mock.Of<IPublishedUrlProvider>(),
                Mock.Of<IUserIdKeyResolver>(),
                Mock.Of<IContentValueNormaliser>(),
                Mock.Of<IAutomationActionAuthorizer>(),
                Mock.Of<IVariationContextAccessor>(),
                Mock.Of<ILogger<GetMediaAction>>()),
            "GetMediaPropertyAction" => new GetMediaPropertyAction(
                infrastructure,
                Mock.Of<IPublishedMediaCache>(),
                Mock.Of<IUmbracoContextFactory>(),
                Mock.Of<IContentValueNormaliser>(),
                Mock.Of<IAutomationActionAuthorizer>(),
                Mock.Of<IVariationContextAccessor>(),
                Mock.Of<ILogger<GetMediaPropertyAction>>()),
            "FindMediaAction" => new FindMediaAction(
                infrastructure,
                Mock.Of<IExamineManager>(),
                Mock.Of<IMediaTypeService>(),
                Mock.Of<IPublishedMediaCache>(),
                Mock.Of<IUmbracoContextFactory>(),
                Mock.Of<IPublishedUrlProvider>(),
                Mock.Of<IAutomationActionAuthorizer>(),
                Mock.Of<ILogger<FindMediaAction>>()),
            "CreateMediaAction" => new CreateMediaAction(
                infrastructure,
                Mock.Of<IMediaService>(),
                Mock.Of<IMediaTypeService>(),
                Mock.Of<IUserIdKeyResolver>(),
                Mock.Of<IBackOfficeSecurityAccessor>(),
                Mock.Of<IUmbracoContextFactory>(),
                Mock.Of<IAutomationActionAuthorizer>(),
                Mock.Of<IMediaFileDownloader>(),
                Mock.Of<ILogger<CreateMediaAction>>()),
            "UpdateMediaPropertyAction" => new UpdateMediaPropertyAction(
                infrastructure,
                Mock.Of<IMediaService>(),
                Mock.Of<IUserIdKeyResolver>(),
                Mock.Of<IBackOfficeSecurityAccessor>(),
                Mock.Of<IUmbracoContextFactory>(),
                Mock.Of<IAutomationActionAuthorizer>(),
                Mock.Of<ILogger<UpdateMediaPropertyAction>>()),
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
            Mock.Of<IVariationContextAccessor>(),
            Mock.Of<ILogger<GetContentAction>>());
}
