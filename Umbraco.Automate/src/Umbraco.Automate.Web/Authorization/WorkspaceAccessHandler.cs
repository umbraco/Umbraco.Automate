using Microsoft.AspNetCore.Authorization;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Api.Management.Security.Authorization;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Extensions;

namespace Umbraco.Automate.Web.Authorization;

/// <summary>
/// Authorization handler that checks whether the current user is a member of a workspace.
/// Membership is determined by overlap between the user's groups and the workspace's configured user groups.
/// Admins bypass the membership check.
/// </summary>
internal sealed class WorkspaceAccessHandler
    : MustSatisfyRequirementAuthorizationHandler<WorkspaceAccessRequirement, WorkspaceAccessResource>
{
    private readonly IAuthorizationHelper _authorizationHelper;
    private readonly IWorkspaceService _workspaceService;

    public WorkspaceAccessHandler(
        IAuthorizationHelper authorizationHelper,
        IWorkspaceService workspaceService)
    {
        _authorizationHelper = authorizationHelper;
        _workspaceService = workspaceService;
    }

    protected override async Task<bool> IsAuthorized(
        AuthorizationHandlerContext context,
        WorkspaceAccessRequirement requirement,
        WorkspaceAccessResource resource)
    {
        if (!_authorizationHelper.TryGetUmbracoUser(context.User, out var user))
        {
            return false;
        }

        // Admins bypass workspace membership checks.
        if (user.IsAdmin())
        {
            return true;
        }

        var workspace = await _workspaceService.GetWorkspaceAsync(resource.WorkspaceId);
        if (workspace is null)
        {
            return false;
        }

        // Check if any of the user's groups overlap with the workspace's configured user groups.
        var userGroupKeys = user.Groups.Select(g => g.Key).ToHashSet();
        return workspace.UserGroups.Any(userGroupKeys.Contains);
    }
}
