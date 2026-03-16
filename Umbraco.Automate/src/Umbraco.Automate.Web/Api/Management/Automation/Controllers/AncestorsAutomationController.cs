using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Automation.Models;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Gets the tree ancestor chain for an automation (workspace → groups, root-first order).
/// </summary>
[ApiVersion("1.0")]
public sealed class AncestorsAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IWorkspaceGroupService _workspaceGroupService;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AncestorsAutomationController"/> class.
    /// </summary>
    public AncestorsAutomationController(
        IAutomationService automationService,
        IWorkspaceService workspaceService,
        IWorkspaceGroupService workspaceGroupService,
        IAuthorizationService authorizationService)
    {
        _automationService = automationService;
        _workspaceService = workspaceService;
        _workspaceGroupService = workspaceGroupService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Gets the tree ancestor chain for an automation, ordered root-first
    /// (workspace, then groups from outermost to innermost).
    /// </summary>
    [HttpGet("{id:guid}/ancestors")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<AutomationAncestorResponseModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAutomationAncestors(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var automation = await _automationService.GetAutomationAsync(id, cancellationToken);
        if (automation is null)
        {
            return AutomationNotFound();
        }

        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, automation.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var ancestors = new List<AutomationAncestorResponseModel>();

        // Add workspace as the first ancestor
        var workspace = await _workspaceService.GetWorkspaceAsync(automation.WorkspaceId, cancellationToken);
        if (workspace is not null)
        {
            ancestors.Add(new AutomationAncestorResponseModel
            {
                Id = workspace.Id,
                EntityType = Constants.EntityType.Workspace,
                Name = workspace.Name,
                ParentId = null,
                ParentEntityType = Constants.EntityType.WorkspaceRoot,
                HasChildren = true,
                IsFolder = false,
            });
        }

        // Walk up the group chain if the automation is in a group
        if (automation.GroupId.HasValue)
        {
            var groupChain = await BuildGroupChainAsync(
                automation.WorkspaceId,
                automation.GroupId.Value,
                cancellationToken);
            ancestors.AddRange(groupChain);
        }

        return Ok(ancestors);
    }

    private async Task<List<AutomationAncestorResponseModel>> BuildGroupChainAsync(
        Guid workspaceId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var chain = new List<AutomationAncestorResponseModel>();
        var currentId = (Guid?)groupId;

        while (currentId.HasValue)
        {
            var group = await _workspaceGroupService.GetGroupAsync(currentId.Value, cancellationToken);
            if (group is null)
            {
                break;
            }

            var parentId = group.ParentId ?? workspaceId;
            var parentEntityType = group.ParentId.HasValue
                ? Constants.EntityType.AutomationGroup
                : Constants.EntityType.Workspace;

            chain.Insert(0, new AutomationAncestorResponseModel
            {
                Id = group.Id,
                EntityType = Constants.EntityType.AutomationGroup,
                Name = group.Name,
                ParentId = parentId,
                ParentEntityType = parentEntityType,
                HasChildren = true,
                IsFolder = true,
            });

            currentId = group.ParentId;
        }

        return chain;
    }
}
