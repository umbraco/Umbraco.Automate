using Umbraco.Cms.Core.Security.Authorization;

namespace Umbraco.Automate.Web.Authorization;

/// <summary>
/// Resource representing a workspace that requires membership authorization.
/// </summary>
public sealed class WorkspaceAccessResource : IPermissionResource
{
    /// <summary>
    /// Gets the workspace ID to authorize access to.
    /// </summary>
    public Guid WorkspaceId { get; }

    private WorkspaceAccessResource(Guid workspaceId)
        => WorkspaceId = workspaceId;

    /// <summary>
    /// Creates a resource for a specific workspace.
    /// </summary>
    public static WorkspaceAccessResource WithId(Guid workspaceId)
        => new(workspaceId);
}
