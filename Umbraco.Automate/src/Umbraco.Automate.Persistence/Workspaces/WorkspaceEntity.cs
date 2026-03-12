namespace Umbraco.Automate.Persistence.Workspaces;

/// <summary>
/// EF Core entity for the workspace table.
/// </summary>
internal sealed class WorkspaceEntity
{
    public Guid Id { get; set; }

    public required string Alias { get; set; }

    public required string Name { get; set; }

    public Guid ServiceAccountKey { get; set; }

    public int Version { get; set; }

    public DateTime DateCreated { get; set; }

    public DateTime DateModified { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? ModifiedByUserId { get; set; }

    public ICollection<WorkspaceUserGroupEntity> UserGroups { get; set; } = [];

    public ICollection<WorkspaceConnectionEntity> AllowedConnections { get; set; } = [];
}
