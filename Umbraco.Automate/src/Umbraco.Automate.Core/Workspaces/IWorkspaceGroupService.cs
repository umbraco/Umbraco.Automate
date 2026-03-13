namespace Umbraco.Automate.Core.Workspaces;

/// <summary>
/// Service for managing workspace groups (folders) within workspaces.
/// </summary>
public interface IWorkspaceGroupService
{
    /// <summary>
    /// Gets a group by its unique ID.
    /// </summary>
    Task<WorkspaceGroup?> GetGroupAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all groups for a workspace.
    /// </summary>
    Task<IEnumerable<WorkspaceGroup>> GetGroupsByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new group.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the workspace or parent group is not found, or a duplicate name exists.</exception>
    Task<WorkspaceGroup> CreateGroupAsync(WorkspaceGroup group, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing group.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the group, parent group, or workspace is not found, or a duplicate name exists.</exception>
    Task<WorkspaceGroup> UpdateGroupAsync(WorkspaceGroup group, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a group and cascade-deletes all child groups and automations within.
    /// </summary>
    Task<bool> DeleteGroupAsync(Guid id, CancellationToken cancellationToken = default);
}
