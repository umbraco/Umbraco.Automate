using Umbraco.Automate.Core.Workspaces;

namespace Umbraco.Automate.Persistence.Workspaces;

/// <summary>
/// Maps between <see cref="Workspace"/> domain model and <see cref="WorkspaceEntity"/> EF entity.
/// </summary>
internal static class WorkspaceFactory
{
    public static Workspace BuildDomain(WorkspaceEntity entity)
    {
        return new Workspace
        {
            Id = entity.Id,
            Alias = entity.Alias,
            Name = entity.Name,
            ServiceAccountKey = entity.ServiceAccountKey,
            UserGroups = entity.UserGroups.Select(ug => ug.UserGroupId).ToList(),
            AllowedConnections = entity.AllowedConnections.Select(c => c.ConnectionId).ToList(),
            Version = entity.Version,
            DateCreated = entity.DateCreated,
            DateModified = entity.DateModified,
            CreatedByUserId = entity.CreatedByUserId,
            ModifiedByUserId = entity.ModifiedByUserId,
        };
    }

    public static WorkspaceEntity BuildEntity(Workspace workspace)
    {
        return new WorkspaceEntity
        {
            Id = workspace.Id,
            Alias = workspace.Alias,
            Name = workspace.Name,
            ServiceAccountKey = workspace.ServiceAccountKey,
            UserGroups = workspace.UserGroups
                .Select(id => new WorkspaceUserGroupEntity { WorkspaceId = workspace.Id, UserGroupId = id })
                .ToList(),
            AllowedConnections = workspace.AllowedConnections
                .Select(id => new WorkspaceConnectionEntity { WorkspaceId = workspace.Id, ConnectionId = id })
                .ToList(),
            Version = workspace.Version,
            DateCreated = workspace.DateCreated,
            DateModified = workspace.DateModified,
            CreatedByUserId = workspace.CreatedByUserId,
            ModifiedByUserId = workspace.ModifiedByUserId,
        };
    }

    public static void UpdateEntity(WorkspaceEntity entity, Workspace workspace)
    {
        entity.Alias = workspace.Alias;
        entity.Name = workspace.Name;
        entity.ServiceAccountKey = workspace.ServiceAccountKey;
        entity.Version = workspace.Version;
        entity.DateModified = workspace.DateModified;
        entity.ModifiedByUserId = workspace.ModifiedByUserId;
        // DateCreated and CreatedByUserId intentionally not updated

        // Replace join table collections
        entity.UserGroups = workspace.UserGroups
            .Select(id => new WorkspaceUserGroupEntity { WorkspaceId = entity.Id, UserGroupId = id })
            .ToList();
        entity.AllowedConnections = workspace.AllowedConnections
            .Select(id => new WorkspaceConnectionEntity { WorkspaceId = entity.Id, ConnectionId = id })
            .ToList();
    }
}
