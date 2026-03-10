namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Service for managing automation lifecycle (CRUD, publish, enable/disable).
/// </summary>
public interface IAutomationService
{
    /// <summary>
    /// Gets an automation by its unique ID.
    /// </summary>
    Task<Automation?> GetAutomationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an automation by its alias.
    /// </summary>
    Task<Automation?> GetAutomationByAliasAsync(string alias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all automations.
    /// </summary>
    Task<IEnumerable<Automation>> GetAllAutomationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of automations.
    /// </summary>
    Task<(IEnumerable<Automation> Items, int Total)> GetAutomationsPagedAsync(
        string? filter = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new automation.
    /// </summary>
    Task<Automation> CreateAutomationAsync(Automation automation, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing automation.
    /// </summary>
    Task<Automation> UpdateAutomationAsync(Automation automation, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an automation, making its current draft the active version.
    /// </summary>
    Task<Automation> PublishAutomationAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unpublishes an automation, setting it to inactive.
    /// </summary>
    Task<Automation> UnpublishAutomationAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an automation and all its runs.
    /// </summary>
    Task<bool> DeleteAutomationAsync(Guid id, CancellationToken cancellationToken = default);
}
