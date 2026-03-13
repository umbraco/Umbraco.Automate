namespace Umbraco.Automate.Persistence.Workspaces;

/// <summary>
/// EF Core entity for the workspace group table.
/// </summary>
internal sealed class WorkspaceGroupEntity
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public Guid? ParentId { get; set; }

    public Guid WorkspaceId { get; set; }

    public DateTime DateCreated { get; set; }
}
