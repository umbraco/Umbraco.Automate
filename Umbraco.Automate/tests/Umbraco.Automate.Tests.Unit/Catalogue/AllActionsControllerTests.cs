using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Mapping;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using ActionContext = Umbraco.Automate.Core.Actions.ActionContext;
using ActionResult = Umbraco.Automate.Core.Actions.ActionResult;

namespace Umbraco.Automate.Tests.Unit.Catalogue;

public class AllActionsControllerTests
{
    private static readonly ActionInfrastructure ActionDeps = new(Mock.Of<IEditableModelResolver>());

    [Fact]
    public async Task Returns_all_actions_when_no_workspace_id_supplied()
    {
        var controller = CreateController(
            actions: [new ContentAction(ActionDeps), new MediaAction(ActionDeps), new UniversalAction(ActionDeps)]);

        var result = await controller.GetAllActions(workspaceId: null);

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        var items = ok.Value.ShouldBeOfType<List<ActionItemResponseModel>>();
        items.Select(i => i.Alias).ShouldBe(["test.contentAction", "test.mediaAction", "test.universalAction"], ignoreOrder: true);
    }

    [Fact]
    public async Task Filters_actions_to_those_satisfied_by_service_account_sections()
    {
        var workspaceId = Guid.NewGuid();
        var serviceAccountKey = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithId(workspaceId)
            .WithAlias("ws")
            .WithName("WS")
            .WithServiceAccountKey(serviceAccountKey)
            .Build();

        var user = UserWithSections("content");

        var controller = CreateController(
            actions: [new ContentAction(ActionDeps), new MediaAction(ActionDeps), new UniversalAction(ActionDeps)],
            workspace: workspace,
            user: user);

        var result = await controller.GetAllActions(workspaceId);

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        var items = ok.Value.ShouldBeOfType<List<ActionItemResponseModel>>();
        items.Select(i => i.Alias).ShouldBe(["test.contentAction", "test.universalAction"], ignoreOrder: true);
    }

    [Fact]
    public async Task Returns_404_when_workspace_does_not_exist()
    {
        var controller = CreateController();

        var result = await controller.GetAllActions(workspaceId: Guid.NewGuid());

        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    private static AllActionsController CreateController(
        IAction[]? actions = null,
        Workspace? workspace = null,
        IUser? user = null)
    {
        var actionCollection = new ActionCollection(() => actions ?? []);
        var mapper = BuildMapper();

        var workspaceService = new Mock<IWorkspaceService>();
        if (workspace is not null)
        {
            workspaceService
                .Setup(s => s.GetWorkspaceAsync(workspace.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(workspace);
        }

        var userService = new Mock<IUserService>();
        if (workspace is not null && workspace.ServiceAccountKey != Guid.Empty)
        {
            userService
                .Setup(s => s.GetAsync(workspace.ServiceAccountKey))
                .ReturnsAsync(user);
        }

        return new AllActionsController(
            actionCollection,
            mapper,
            workspaceService.Object,
            userService.Object,
            new SectionAccessChecker());
    }

    private static IUmbracoMapper BuildMapper()
    {
        var mapDefinition = new CatalogueMapDefinition();
        var definitions = new MapDefinitionCollection(() => [mapDefinition]);
        return new UmbracoMapper(
            definitions,
            Mock.Of<ICoreScopeProvider>(),
            Mock.Of<ILogger<UmbracoMapper>>());
    }

    private static IUser UserWithSections(params string[] sections)
    {
        var mock = new Mock<IUser>();
        mock.Setup(u => u.AllowedSections).Returns(sections);
        return mock.Object;
    }

    #region Test actions

    [Action("test.contentAction", "Content", RequiredSections = ["content"])]
    private sealed class ContentAction(ActionInfrastructure infrastructure) : ActionBase<object, object>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.mediaAction", "Media", RequiredSections = ["media"])]
    private sealed class MediaAction(ActionInfrastructure infrastructure) : ActionBase<object, object>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.universalAction", "Universal")]
    private sealed class UniversalAction(ActionInfrastructure infrastructure) : ActionBase<object, object>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    #endregion
}
