namespace Umbraco.Automate.Core.Workspaces;

/// <summary>
/// Repository for workspace persistence. Internal implementation detail of <c>IWorkspaceService</c>.
/// </summary>
internal interface IWorkspaceRepository
{
    /// <summary>
    /// Gets a workspace by its unique ID.
    /// </summary>
    Task<Workspace?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workspace by its alias.
    /// </summary>
    Task<Workspace?> GetByAliasAsync(string alias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all workspaces.
    /// </summary>
    Task<IEnumerable<Workspace>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of workspaces.
    /// </summary>
    Task<(IEnumerable<Workspace> Items, int Total)> GetPagedAsync(
        string? filter = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a workspace (insert or update).
    /// </summary>
    Task<Workspace> SaveAsync(Workspace workspace, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a workspace by its ID.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a workspace with the given ID exists.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
