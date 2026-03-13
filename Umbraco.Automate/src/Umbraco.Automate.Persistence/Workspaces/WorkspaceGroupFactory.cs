using Umbraco.Automate.Core.Workspaces;

namespace Umbraco.Automate.Persistence.Workspaces;

/// <summary>
/// Maps between <see cref="WorkspaceGroup"/> domain model and <see cref="WorkspaceGroupEntity"/> EF entity.
/// </summary>
internal sealed class WorkspaceGroupFactory
{
    public WorkspaceGroup BuildDomain(WorkspaceGroupEntity entity)
    {
        return new WorkspaceGroup
        {
            Id = entity.Id,
            Name = entity.Name,
            ParentId = entity.ParentId,
            WorkspaceId = entity.WorkspaceId,
            DateCreated = entity.DateCreated,
        };
    }

    public WorkspaceGroupEntity BuildEntity(WorkspaceGroup group)
    {
        return new WorkspaceGroupEntity
        {
            Id = group.Id,
            Name = group.Name,
            ParentId = group.ParentId,
            WorkspaceId = group.WorkspaceId,
            DateCreated = group.DateCreated,
        };
    }

    public void UpdateEntity(WorkspaceGroupEntity entity, WorkspaceGroup group)
    {
        entity.Name = group.Name;
        entity.ParentId = group.ParentId;
        // WorkspaceId and DateCreated intentionally not updated
    }
}
