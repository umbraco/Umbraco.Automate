using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Mapping;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Tests.Unit.Catalogue;

public class AllTriggersControllerTests
{
    private static readonly TriggerInfrastructure TriggerDeps = new(Mock.Of<IEditableModelResolver>());

    [Fact]
    public async Task Returns_all_triggers_when_no_workspace_id_supplied()
    {
        var controller = CreateController(
            triggers: [new ContentTrigger(TriggerDeps), new UsersTrigger(TriggerDeps), new UniversalTrigger(TriggerDeps)]);

        var result = await controller.GetAllTriggers(workspaceId: null);

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        var items = ok.Value.ShouldBeOfType<List<TriggerItemResponseModel>>();
        items.Select(i => i.Alias).ShouldBe(["test.contentTrigger", "test.usersTrigger", "test.universalTrigger"], ignoreOrder: true);
    }

    [Fact]
    public async Task Filters_triggers_to_those_satisfied_by_service_account_sections()
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
            triggers: [new ContentTrigger(TriggerDeps), new UsersTrigger(TriggerDeps), new UniversalTrigger(TriggerDeps)],
            workspace: workspace,
            user: user);

        var result = await controller.GetAllTriggers(workspaceId);

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        var items = ok.Value.ShouldBeOfType<List<TriggerItemResponseModel>>();
        items.Select(i => i.Alias).ShouldBe(["test.contentTrigger", "test.universalTrigger"], ignoreOrder: true);
    }

    [Fact]
    public async Task Returns_404_when_workspace_does_not_exist()
    {
        var controller = CreateController();

        var result = await controller.GetAllTriggers(workspaceId: Guid.NewGuid());

        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Returns_404_when_service_account_does_not_exist()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithId(workspaceId)
            .WithAlias("ws")
            .WithName("WS")
            .WithServiceAccountKey(Guid.NewGuid())
            .Build();

        var controller = CreateController(workspace: workspace, user: null);

        var result = await controller.GetAllTriggers(workspaceId);

        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Returns_404_when_workspace_has_no_service_account_configured()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithId(workspaceId)
            .WithAlias("ws")
            .WithName("WS")
            .WithServiceAccountKey(Guid.Empty)
            .Build();

        var controller = CreateController(workspace: workspace);

        var result = await controller.GetAllTriggers(workspaceId);

        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    private static AllTriggersController CreateController(
        ITrigger[]? triggers = null,
        Workspace? workspace = null,
        IUser? user = null)
    {
        var triggerCollection = new TriggerCollection(() => triggers ?? []);
        var mapper = BuildMapper();

        var serviceAccountResolver = new Mock<IWorkspaceServiceAccountResolver>();
        if (workspace is not null && workspace.ServiceAccountKey != Guid.Empty)
        {
            serviceAccountResolver
                .Setup(r => r.GetServiceAccountAsync(workspace.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
        }

        return new AllTriggersController(
            triggerCollection,
            mapper,
            serviceAccountResolver.Object,
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

    #region Test triggers

    [Trigger("test.contentTrigger", "Content", RequiredSections = ["content"])]
    private sealed class ContentTrigger(TriggerInfrastructure infrastructure) : TriggerBase<object, object>(infrastructure);

    [Trigger("test.usersTrigger", "Users", RequiredSections = ["users"])]
    private sealed class UsersTrigger(TriggerInfrastructure infrastructure) : TriggerBase<object, object>(infrastructure);

    [Trigger("test.universalTrigger", "Universal")]
    private sealed class UniversalTrigger(TriggerInfrastructure infrastructure) : TriggerBase<object, object>(infrastructure);

    #endregion
}
