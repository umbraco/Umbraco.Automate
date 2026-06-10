using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Security;

/// <inheritdoc />
internal sealed class WorkspaceServiceAccountResolver : IWorkspaceServiceAccountResolver
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IUserService _userService;

    public WorkspaceServiceAccountResolver(IWorkspaceService workspaceService, IUserService userService)
    {
        _workspaceService = workspaceService;
        _userService = userService;
    }

    /// <inheritdoc />
    public async Task<IUser?> GetServiceAccountAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceService.GetWorkspaceAsync(workspaceId, cancellationToken);
        if (workspace is null || workspace.ServiceAccountKey == Guid.Empty)
        {
            return null;
        }

        return await _userService.GetAsync(workspace.ServiceAccountKey);
    }
}
