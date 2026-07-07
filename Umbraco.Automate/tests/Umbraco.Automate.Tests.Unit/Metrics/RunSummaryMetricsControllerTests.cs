using System.Security.Principal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Metrics.Controllers;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security.Authorization;

namespace Umbraco.Automate.Tests.Unit.Metrics;

public class RunSummaryMetricsControllerTests
{
    private readonly Mock<IAutomationRunService> _runService = new();
    private readonly Mock<IWorkspaceService> _workspaceService = new();
    private readonly Mock<IAuthorizationHelper> _authorizationHelper = new();
    private readonly RunSummaryMetricsController _controller;

    public RunSummaryMetricsControllerTests()
    {
        _controller = new RunSummaryMetricsController(
            _runService.Object,
            _workspaceService.Object,
            _authorizationHelper.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task GetRunSummary_Admin_SummarisesAllWorkspaces()
    {
        _runService
            .Setup(s => s.GetRunSummaryAsync(It.IsAny<IReadOnlySet<Guid>?>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunSummary { TotalRuns = 5 });
        SetupUser(CreateUser(isAdmin: true));

        await _controller.GetRunSummary(cancellationToken: CancellationToken.None);

        _workspaceService.Verify(
            s => s.GetAccessibleWorkspaceIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _runService.Verify(
            s => s.GetRunSummaryAsync(It.Is<IReadOnlySet<Guid>?>(w => w == null), null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRunSummary_NonAdmin_ScopesToAccessibleWorkspaces()
    {
        var accessible = new HashSet<Guid> { Guid.NewGuid() };
        _runService
            .Setup(s => s.GetRunSummaryAsync(It.IsAny<IReadOnlySet<Guid>?>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunSummary());
        SetupUser(CreateUser(isAdmin: false, groupKeys: [Guid.NewGuid()]));
        _workspaceService
            .Setup(s => s.GetAccessibleWorkspaceIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessible);

        await _controller.GetRunSummary(cancellationToken: CancellationToken.None);

        _runService.Verify(
            s => s.GetRunSummaryAsync(accessible, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRunSummary_NonAdminWithWorkspaceFilter_IntersectsWithAccessible()
    {
        var accessible = Guid.NewGuid();
        var inaccessible = Guid.NewGuid();
        _runService
            .Setup(s => s.GetRunSummaryAsync(It.IsAny<IReadOnlySet<Guid>?>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunSummary());
        SetupUser(CreateUser(isAdmin: false, groupKeys: [Guid.NewGuid()]));
        _workspaceService
            .Setup(s => s.GetAccessibleWorkspaceIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { accessible });

        // Request a workspace the user cannot access -> intersection is empty.
        await _controller.GetRunSummary(workspaceId: inaccessible, cancellationToken: CancellationToken.None);

        _runService.Verify(
            s => s.GetRunSummaryAsync(
                It.Is<IReadOnlySet<Guid>?>(w => w != null && w.Count == 0),
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRunCountsByAutomation_NonAdmin_ScopesToAccessibleWorkspaces()
    {
        var accessible = new HashSet<Guid> { Guid.NewGuid() };
        _runService
            .Setup(s => s.GetRunCountsByAutomationAsync(
                It.IsAny<IReadOnlySet<Guid>?>(), null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        SetupUser(CreateUser(isAdmin: false, groupKeys: [Guid.NewGuid()]));
        _workspaceService
            .Setup(s => s.GetAccessibleWorkspaceIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessible);

        await _controller.GetRunCountsByAutomation(cancellationToken: CancellationToken.None);

        _runService.Verify(
            s => s.GetRunCountsByAutomationAsync(accessible, null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
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
