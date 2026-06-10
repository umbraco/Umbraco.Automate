namespace Umbraco.Automate.Core.Workspaces;

/// <summary>
/// Service for managing workspace lifecycle (CRUD).
/// </summary>
public interface IWorkspaceService
{
    /// <summary>
    /// Gets a workspace by its unique ID.
    /// </summary>
    Task<Workspace?> GetWorkspaceAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workspace by its alias.
    /// </summary>
    Task<Workspace?> GetWorkspaceByAliasAsync(string alias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all workspaces.
    /// </summary>
    Task<IEnumerable<Workspace>> GetAllWorkspacesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of workspaces. When <paramref name="userGroupKeys"/> is provided,
    /// only workspaces accessible to those user groups are returned.
    /// </summary>
    Task<(IEnumerable<Workspace> Items, int Total)> GetWorkspacesPagedAsync(
        string? filter = null,
        IReadOnlyCollection<Guid>? userGroupKeys = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new workspace.
    /// </summary>
    Task<Workspace> CreateWorkspaceAsync(Workspace workspace, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing workspace.
    /// </summary>
    Task<Workspace> UpdateWorkspaceAsync(Workspace workspace, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a workspace.
    /// </summary>
    Task<bool> DeleteWorkspaceAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back a workspace to a previous version snapshot.
    /// </summary>
    Task<Workspace> RollbackWorkspaceAsync(
        Guid workspaceId,
        int targetVersion,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the IDs of workspaces accessible to any of the given user group keys.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetAccessibleWorkspaceIdsAsync(
        IEnumerable<Guid> userGroupKeys,
        CancellationToken cancellationToken = default);
}
