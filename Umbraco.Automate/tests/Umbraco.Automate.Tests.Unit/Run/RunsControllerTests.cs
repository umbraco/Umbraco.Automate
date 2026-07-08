using System.Security.Principal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Run.Controllers;
using Umbraco.Automate.Web.Api.Management.Run.Mapping;
using Umbraco.Automate.Web.Api.Management.Run.Models;
using Umbraco.Cms.Api.Common.ViewModels.Pagination;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Security.Authorization;

namespace Umbraco.Automate.Tests.Unit.Run;

public class RunsControllerTests
{
    private readonly Mock<IAutomationRunService> _runService = new();
    private readonly Mock<IWorkspaceService> _workspaceService = new();
    private readonly Mock<IAuthorizationHelper> _authorizationHelper = new();
    private readonly RunsController _controller;

    public RunsControllerTests()
    {
        _controller = new RunsController(
            _runService.Object,
            _workspaceService.Object,
            _authorizationHelper.Object,
            BuildMapper());

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task GetRuns_Admin_QueriesAllWorkspacesAndMapsItems()
    {
        var item = ListItem("Publish blog");
        SetupRuns(items: [item], total: 1);
        SetupUser(CreateUser(isAdmin: true));

        var paged = await GetPaged();

        paged.Total.ShouldBe(1);
        paged.Items.Single().AutomationName.ShouldBe("Publish blog");

        // Admins are not scoped, so no workspace lookup and a null filter to the service.
        _workspaceService.Verify(
            s => s.GetAccessibleWorkspaceIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _runService.Verify(
            s => s.GetRunsPagedAsync(It.Is<IReadOnlySet<Guid>?>(w => w == null), 0, 100, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRuns_NonAdmin_ScopesToAccessibleWorkspaces()
    {
        var accessible = new HashSet<Guid> { Guid.NewGuid() };
        var groupKey = Guid.NewGuid();
        SetupRuns(items: [ListItem("A")], total: 1);
        SetupUser(CreateUser(isAdmin: false, groupKeys: [groupKey]));
        _workspaceService
            .Setup(s => s.GetAccessibleWorkspaceIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessible);

        var paged = await GetPaged();

        paged.Total.ShouldBe(1);
        _runService.Verify(
            s => s.GetRunsPagedAsync(accessible, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRuns_PassesPagingThrough()
    {
        SetupRuns(items: [], total: 0);
        SetupUser(CreateUser(isAdmin: true));

        await _controller.GetRuns(skip: 40, take: 20, CancellationToken.None);

        _runService.Verify(
            s => s.GetRunsPagedAsync(It.IsAny<IReadOnlySet<Guid>?>(), 40, 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private async Task<PagedViewModel<AutomationRunListItemResponseModel>> GetPaged()
    {
        var result = await _controller.GetRuns(0, 100, CancellationToken.None);
        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        return ok.Value.ShouldBeOfType<PagedViewModel<AutomationRunListItemResponseModel>>();
    }

    private void SetupRuns(IReadOnlyList<AutomationRunListItem> items, int total)
        => _runService
            .Setup(s => s.GetRunsPagedAsync(
                It.IsAny<IReadOnlySet<Guid>?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, total));

    private static AutomationRunListItem ListItem(string automationName) => new()
    {
        Id = Guid.NewGuid(),
        AutomationId = Guid.NewGuid(),
        AutomationName = automationName,
        AutomationVersion = 1,
        Status = AutomationRunStatus.Completed,
        StartedUtc = DateTime.UtcNow,
        CompletedUtc = DateTime.UtcNow,
        InitiatedBy = "manual",
    };

    private static IUmbracoMapper BuildMapper()
    {
        var definitions = new MapDefinitionCollection(() => [new RunMapDefinition()]);
        return new UmbracoMapper(
            definitions,
            Mock.Of<ICoreScopeProvider>(),
            Mock.Of<ILogger<UmbracoMapper>>());
    }

    private void SetupUser(IUser user)
        => _authorizationHelper.Setup(h => h.GetUmbracoUser(It.IsAny<IPrincipal>())).Returns(user);

    private static IUser CreateUser(bool isAdmin, IEnumerable<Guid>? groupKeys = null)
    {
        var user = new Mock<IUser>();
        var groups = new List<IReadOnlyUserGroup>();

        if (isAdmin)
        {
            var adminGroup = new Mock<IReadOnlyUserGroup>();
            adminGroup.Setup(g => g.Alias).Returns(Umbraco.Cms.Core.Constants.Security.AdminGroupAlias);
            adminGroup.Setup(g => g.Key).Returns(Guid.NewGuid());
            groups.Add(adminGroup.Object);
        }

        foreach (var key in groupKeys ?? [])
        {
            var group = new Mock<IReadOnlyUserGroup>();
            group.Setup(g => g.Alias).Returns($"group-{key:N}");
            group.Setup(g => g.Key).Returns(key);
            groups.Add(group.Object);
        }

        user.Setup(u => u.Groups).Returns(groups);
        return user.Object;
    }
}
