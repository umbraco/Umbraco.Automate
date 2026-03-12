namespace Umbraco.Automate.Persistence.Workspaces;

/// <summary>
/// Join table entity linking a workspace to a connection.
/// </summary>
internal sealed class WorkspaceConnectionEntity
{
    public Guid WorkspaceId { get; set; }

    public Guid ConnectionId { get; set; }
}
