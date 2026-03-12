using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Authorization;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security.Authorization;

namespace Umbraco.Automate.Tests.Unit.Authorization;

public class WorkspaceAccessHandlerTests
{
    private readonly Mock<IAuthorizationHelper> _authHelper = new();
    private readonly Mock<IWorkspaceService> _workspaceService = new();
    private readonly WorkspaceAccessHandler _handler;

    public WorkspaceAccessHandlerTests()
    {
        _handler = new WorkspaceAccessHandler(_authHelper.Object, _workspaceService.Object);
    }

    [Fact]
    public async Task Denies_WhenUserCannotBeResolved()
    {
        _authHelper.Setup(h => h.TryGetUmbracoUser(It.IsAny<IPrincipal>(), out It.Ref<IUser?>.IsAny))
            .Returns(false);

        var result = await HandleAsync(Guid.NewGuid());

        result.HasFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task Allows_AdminUser()
    {
        var workspaceId = Guid.NewGuid();
        SetupUserResolution(CreateUser(isAdmin: true));

        var result = await HandleAsync(workspaceId);

        result.HasSucceeded.ShouldBeTrue();
        _workspaceService.Verify(s => s.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Denies_WhenWorkspaceNotFound()
    {
        var workspaceId = Guid.NewGuid();
        SetupUserResolution(CreateUser(isAdmin: false, groupKeys: [Guid.NewGuid()]));
        _workspaceService.Setup(s => s.GetWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);

        var result = await HandleAsync(workspaceId);

        result.HasFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task Allows_WhenUserGroupOverlaps()
    {
        var sharedGroupKey = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        SetupUserResolution(CreateUser(isAdmin: false, groupKeys: [sharedGroupKey, Guid.NewGuid()]));
        _workspaceService.Setup(s => s.GetWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Workspace
            {
                Alias = "test",
                Name = "Test",
                UserGroups = [sharedGroupKey, Guid.NewGuid()],
            });

        var result = await HandleAsync(workspaceId);

        result.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Denies_WhenNoGroupOverlap()
    {
        var workspaceId = Guid.NewGuid();
        SetupUserResolution(CreateUser(isAdmin: false, groupKeys: [Guid.NewGuid()]));
        _workspaceService.Setup(s => s.GetWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Workspace
            {
                Alias = "test",
                Name = "Test",
                UserGroups = [Guid.NewGuid()],
            });

        var result = await HandleAsync(workspaceId);

        result.HasFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task Denies_WhenWorkspaceHasNoUserGroups()
    {
        var workspaceId = Guid.NewGuid();
        SetupUserResolution(CreateUser(isAdmin: false, groupKeys: [Guid.NewGuid()]));
        _workspaceService.Setup(s => s.GetWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Workspace
            {
                Alias = "test",
                Name = "Test",
                UserGroups = [],
            });

        var result = await HandleAsync(workspaceId);

        result.HasFailed.ShouldBeTrue();
    }

    private async Task<AuthorizationHandlerContext> HandleAsync(Guid workspaceId)
    {
        var requirement = new WorkspaceAccessRequirement();
        var resource = WorkspaceAccessResource.WithId(workspaceId);
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource);

        await ((IAuthorizationHandler)_handler).HandleAsync(context);
        return context;
    }

    private delegate void TryGetCallback(IPrincipal principal, out IUser? user);

    private void SetupUserResolution(IUser user)
    {
        _authHelper.Setup(h => h.TryGetUmbracoUser(It.IsAny<IPrincipal>(), out It.Ref<IUser?>.IsAny))
            .Callback(new TryGetCallback((IPrincipal _, out IUser? u) => u = user))
            .Returns(true);
    }

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

        if (groupKeys is not null)
        {
            foreach (var key in groupKeys)
            {
                var group = new Mock<IReadOnlyUserGroup>();
                group.Setup(g => g.Alias).Returns($"group-{key:N}");
                group.Setup(g => g.Key).Returns(key);
                groups.Add(group.Object);
            }
        }

        user.Setup(u => u.Groups).Returns(groups);
        return user.Object;
    }
}
