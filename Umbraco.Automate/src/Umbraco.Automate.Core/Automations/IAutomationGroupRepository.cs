namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Repository for automation group persistence. Internal implementation detail of <c>IAutomationGroupService</c>.
/// </summary>
internal interface IAutomationGroupRepository
{
    /// <summary>
    /// Gets a group by its unique ID.
    /// </summary>
    Task<AutomationGroup?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all groups for a workspace.
    /// </summary>
    Task<IEnumerable<AutomationGroup>> GetByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a group (insert or update).
    /// </summary>
    Task<AutomationGroup> SaveAsync(AutomationGroup group, CancellationToken cancellationToken = default);

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
