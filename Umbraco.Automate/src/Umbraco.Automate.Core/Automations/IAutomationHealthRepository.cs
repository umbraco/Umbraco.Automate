namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Repository for per-automation circuit-breaker health state. Internal implementation detail
/// of the circuit breaker service.
/// </summary>
internal interface IAutomationHealthRepository
{
    /// <summary>
    /// Gets the health state for an automation, or null if no row exists (implicitly Healthy).
    /// </summary>
    Task<AutomationHealthState?> GetAsync(Guid automationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates the health state for an automation.
    /// </summary>
    Task SaveAsync(AutomationHealthState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets health states for the given automations, keyed by automation id. Ids with no row are
    /// omitted (implicitly Healthy).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, AutomationHealthState>> GetManyAsync(
        IReadOnlyCollection<Guid> automationIds,
        CancellationToken cancellationToken = default);
}
