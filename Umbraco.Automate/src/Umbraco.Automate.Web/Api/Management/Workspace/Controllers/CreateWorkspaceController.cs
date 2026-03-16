using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Workspace.Models;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Controllers;

/// <summary>
/// Creates a new workspace.
/// </summary>
[ApiVersion("1.0")]
public sealed class CreateWorkspaceController : WorkspaceControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateWorkspaceController"/> class.
    /// </summary>
    public CreateWorkspaceController(
        IWorkspaceService workspaceService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoMapper mapper)
    {
        _workspaceService = workspaceService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _mapper = mapper;
    }

    /// <summary>
    /// Creates a new workspace.
    /// </summary>
    [HttpPost]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateWorkspace(
        CreateWorkspaceRequestModel requestModel,
        CancellationToken cancellationToken = default)
    {
        var workspace = _mapper.Map<Core.Workspaces.Workspace>(requestModel)!;

        var created = await _workspaceService.CreateWorkspaceAsync(workspace, CurrentUserKey(_backOfficeSecurityAccessor), cancellationToken);

        return CreatedAtAction(
            nameof(ByIdWorkspaceController.GetWorkspaceById),
            nameof(ByIdWorkspaceController).Replace("Controller", string.Empty),
            new { id = created.Id },
            created.Id.ToString());
    }
}
