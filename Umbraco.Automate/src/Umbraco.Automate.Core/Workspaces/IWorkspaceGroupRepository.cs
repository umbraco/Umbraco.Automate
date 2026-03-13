namespace Umbraco.Automate.Core.Workspaces;

/// <summary>
/// Repository for workspace group persistence. Internal implementation detail of <c>IWorkspaceGroupService</c>.
/// </summary>
internal interface IWorkspaceGroupRepository
{
    /// <summary>
    /// Gets a group by its unique ID.
    /// </summary>
    Task<WorkspaceGroup?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all groups for a workspace.
    /// </summary>
    Task<IEnumerable<WorkspaceGroup>> GetByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a group (insert or update).
    /// </summary>
    Task<WorkspaceGroup> SaveAsync(WorkspaceGroup group, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a group by its ID.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all child group IDs (direct children only).
    /// </summary>
    Task<IEnumerable<Guid>> GetChildIdsAsync(Guid parentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a group with the same name exists at the same level within the same workspace.
    /// </summary>
    Task<bool> NameExistsAsync(Guid workspaceId, Guid? parentId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all groups for a workspace.
    /// </summary>
    Task<int> DeleteByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}
