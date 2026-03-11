namespace Umbraco.Automate.Persistence.Workspaces;

/// <summary>
/// Join table entity linking a workspace to a CMS user group.
/// </summary>
internal sealed class WorkspaceUserGroupEntity
{
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// CMS user group key — external reference, no FK constraint.
    /// </summary>
    public Guid UserGroupId { get; set; }
}
