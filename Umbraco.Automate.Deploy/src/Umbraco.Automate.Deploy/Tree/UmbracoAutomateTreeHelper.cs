using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core;
using Umbraco.Deploy.Core.Environments;

namespace Umbraco.Automate.Deploy.Tree;

/// <summary>
/// Provides remote-tree builders for Umbraco Automate entities, used by Deploy's
/// partial-restore dialog to let editors pick specific items to restore from a remote environment.
/// </summary>
internal static class UmbracoAutomateTreeHelper
{
    /// <summary>
    /// Builds the Automations tree: workspaces → groups &amp; automations.
    /// </summary>
    public static IEnumerable<RemoteTreeEntity> GetAutomationTree(string parentId, string entityType, HttpContext httpContext)
    {
        if (entityType == UmbracoAutomateDeployConstants.UdiEntityType.Automation &&
            parentId == Constants.System.RootString)
        {
            return ListWorkspaces(httpContext);
        }

        if (entityType == UmbracoAutomateDeployConstants.UdiEntityType.Workspace &&
            Guid.TryParse(parentId, out var workspaceId))
        {
            return ListWorkspaceChildren(httpContext, workspaceId, groupId: null, includeAutomations: true);
        }

        if (entityType == UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup &&
            Guid.TryParse(parentId, out var groupId))
        {
            var (workspaceIdForGroup, _) = ResolveGroupContext(httpContext, groupId);
            if (workspaceIdForGroup is null)
            {
                return [];
            }

            return ListWorkspaceChildren(httpContext, workspaceIdForGroup.Value, groupId, includeAutomations: true);
        }

        return [];
    }

    /// <summary>
    /// Builds the Workspaces tree: a flat list of workspaces at the root.
    /// </summary>
    public static IEnumerable<RemoteTreeEntity> GetWorkspaceTree(string parentId, string entityType, HttpContext httpContext)
    {
        if (entityType == UmbracoAutomateDeployConstants.UdiEntityType.Workspace &&
            parentId == Constants.System.RootString)
        {
            var workspaceService = httpContext.RequestServices.GetRequiredService<IWorkspaceService>();
            var workspaces = workspaceService.GetAllWorkspacesAsync().GetAwaiter().GetResult();

            return workspaces.OrderBy(w => w.Name).Select(w => new RemoteTreeEntity
            {
                Id = w.Id.ToString(),
                Title = w.Name,
                Icon = "icon-workspace",
                ParentId = parentId,
                HasChildren = false,
                EntityType = UmbracoAutomateDeployConstants.UdiEntityType.Workspace,
            });
        }

        return [];
    }

    /// <summary>
    /// Builds the Connections tree: a flat list of connections at the root.
    /// </summary>
    public static IEnumerable<RemoteTreeEntity> GetConnectionTree(string parentId, string entityType, HttpContext httpContext)
    {
        if (entityType == UmbracoAutomateDeployConstants.UdiEntityType.Connection &&
            parentId == Constants.System.RootString)
        {
            var connectionService = httpContext.RequestServices.GetRequiredService<IConnectionService>();
            var connections = connectionService.GetAllConnectionsAsync().GetAwaiter().GetResult();

            return connections.OrderBy(c => c.Name).Select(c => new RemoteTreeEntity
            {
                Id = c.Id.ToString(),
                Title = c.Name,
                Icon = "icon-link",
                ParentId = parentId,
                HasChildren = false,
                EntityType = UmbracoAutomateDeployConstants.UdiEntityType.Connection,
            });
        }

        return [];
    }

    private static IEnumerable<RemoteTreeEntity> ListWorkspaces(HttpContext httpContext)
    {
        var workspaceService = httpContext.RequestServices.GetRequiredService<IWorkspaceService>();
        var workspaces = workspaceService.GetAllWorkspacesAsync().GetAwaiter().GetResult();

        return workspaces.OrderBy(w => w.Name).Select(w => new RemoteTreeEntity
        {
            Id = w.Id.ToString(),
            Title = w.Name,
            Icon = "icon-workspace",
            ParentId = Constants.System.RootString,
            HasChildren = true,
            EntityType = UmbracoAutomateDeployConstants.UdiEntityType.Workspace,
        });
    }

    private static IEnumerable<RemoteTreeEntity> ListWorkspaceChildren(
        HttpContext httpContext,
        Guid workspaceId,
        Guid? groupId,
        bool includeAutomations)
    {
        var items = new List<RemoteTreeEntity>();
        var parentId = groupId?.ToString() ?? workspaceId.ToString();

        var groupService = httpContext.RequestServices.GetRequiredService<IWorkspaceGroupService>();
        var groups = groupService.GetGroupsByWorkspaceAsync(workspaceId, groupId).GetAwaiter().GetResult();

        items.AddRange(groups.OrderBy(g => g.Name).Select(g => new RemoteTreeEntity
        {
            Id = g.Id.ToString(),
            Title = g.Name,
            Icon = "icon-folder",
            ParentId = parentId,
            HasChildren = true,
            EntityType = UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup,
        }));

        if (includeAutomations)
        {
            var automationService = httpContext.RequestServices.GetRequiredService<IAutomationService>();
            var filterGroupId = groupId ?? Guid.Empty;
            var (automations, _) = automationService
                .GetAutomationsPagedAsync(
                    filter: null,
                    workspaceIds: new HashSet<Guid> { workspaceId },
                    groupId: filterGroupId,
                    skip: 0,
                    take: int.MaxValue)
                .GetAwaiter().GetResult();

            items.AddRange(automations.OrderBy(a => a.Name).Select(a => new RemoteTreeEntity
            {
                Id = a.Id.ToString(),
                Title = a.Name,
                Icon = "icon-automation",
                ParentId = parentId,
                HasChildren = false,
                EntityType = UmbracoAutomateDeployConstants.UdiEntityType.Automation,
            }));
        }

        return items;
    }

    private static (Guid? WorkspaceId, Guid? ParentGroupId) ResolveGroupContext(HttpContext httpContext, Guid groupId)
    {
        var groupService = httpContext.RequestServices.GetRequiredService<IWorkspaceGroupService>();
        var group = groupService.GetGroupAsync(groupId).GetAwaiter().GetResult();
        return group is null ? (null, null) : (group.WorkspaceId, group.ParentId);
    }
}
