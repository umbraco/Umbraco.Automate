namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Repository for automation persistence. Internal implementation detail of <c>IAutomationService</c>.
/// </summary>
internal interface IAutomationRepository
{
    /// <summary>
    /// Gets an automation by its unique ID.
    /// </summary>
    Task<Automation?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an automation by its alias.
    /// </summary>
    Task<Automation?> GetByAliasAsync(string alias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all automations.
    /// </summary>
    Task<IEnumerable<Automation>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of automations, optionally scoped to specific workspace IDs.
    /// </summary>
    /// <param name="groupId">
    /// When not null, filters by group.
    /// Pass <see cref="Guid.Empty"/> to get root-level automations (those with no group).
    /// </param>
    Task<(IEnumerable<Automation> Items, int Total)> GetPagedAsync(
        string? filter = null,
        IReadOnlySet<Guid>? workspaceIds = null,
        Guid? groupId = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an automation (insert or update).
    /// </summary>
    Task<Automation> SaveAsync(Automation automation, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an automation by its ID.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an automation with the given ID exists.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the published version references for all automations that have a published version.
    /// This is a lightweight projection — no full entity mapping.
    /// </summary>
    Task<IReadOnlyCollection<(Guid Id, int PublishedVersion)>> GetPublishedVersionReferencesAsync(
        CancellationToken cancellationToken = default);
}
