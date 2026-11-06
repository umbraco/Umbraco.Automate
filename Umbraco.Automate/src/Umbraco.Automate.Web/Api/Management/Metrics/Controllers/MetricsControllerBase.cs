using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Common.Controllers;
using Umbraco.Automate.Web.Api.Management.Common.Routing;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Extensions;

namespace Umbraco.Automate.Web.Api.Management.Metrics.Controllers;

/// <summary>
/// Base controller for metrics endpoints.
/// </summary>
[ApiExplorerSettings(GroupName = Constants.ManagementApi.Feature.Metrics.GroupName)]
[UmbracoAutomateVersionedManagementApiRoute(Constants.ManagementApi.Feature.Metrics.RouteSegment)]
public abstract class MetricsControllerBase : UmbracoAutomateManagementControllerBase
{
    /// <summary>
    /// Resolves the set of workspaces to scope metrics to. Admins are unscoped (<c>null</c> = all);
    /// non-admins are limited to their accessible workspaces. An explicit <paramref name="workspaceId"/>
    /// filter is intersected with the accessible set.
    /// </summary>
    /// <param name="workspaceService">The workspace service used to look up accessible workspaces.</param>
    /// <param name="authorizationHelper">The helper used to resolve the current backoffice user.</param>
    /// <param name="workspaceId">Optional explicit workspace filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private protected async Task<IReadOnlySet<Guid>?> ResolveAccessibleWorkspacesAsync(
        IWorkspaceService workspaceService,
        IAuthorizationHelper authorizationHelper,
        Guid? workspaceId,
        CancellationToken cancellationToken)
    {
        IReadOnlySet<Guid>? workspaceIds = null;

        var user = authorizationHelper.GetUmbracoUser(User);
        if (!user.IsAdmin())
        {
            var userGroupKeys = user.Groups.Select(g => g.Key).ToList();
            workspaceIds = await workspaceService.GetAccessibleWorkspaceIdsAsync(userGroupKeys, cancellationToken);
        }

        if (workspaceId.HasValue)
        {
            var requested = new HashSet<Guid> { workspaceId.Value };
            workspaceIds = workspaceIds is not null
                ? (IReadOnlySet<Guid>)requested.Intersect(workspaceIds).ToHashSet()
                : requested;
        }

        return workspaceIds;
    }
}
