using Umbraco.Automate.Core.Runs;

namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Detects "sick" automations (those failing on every trigger due to misconfiguration), warns the
/// owner, and auto-disables them. Health is a separate axis from the publish lifecycle
/// (<see cref="AutomationStatus"/>): the breaker owns <see cref="AutomationHealth"/> and never
/// touches status. Enforced at the single run chokepoint (<c>IAutomationExecutor.ExecuteAsync</c>).
/// </summary>
public interface ICircuitBreakerService
{
    /// <summary>
    /// Evaluates an automation's health after a run reaches a terminal state. May clear a warning,
    /// issue a warning, or auto-disable. Idempotent; safe to call from the run-completed handler.
    /// </summary>
    Task EvaluateAsync(AutomationRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// The run-start gate. Returns false when the automation is auto-disabled, unless the run is a
    /// permitted interactive test (see <see cref="CircuitBreakerOptions.AllowManualRunWhileDisabled"/>
    /// and <see cref="Triggers.TriggerInitiatorType.IsInteractive"/>).
    /// </summary>
    Task<bool> IsRunAllowedAsync(Guid automationId, string initiatorType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets an automation's health back to Healthy. Called on an explicit re-enable and on
    /// re-publish. Never called automatically off the back of a run.
    /// </summary>
    Task ResetAsync(Guid automationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the health state for an automation (Healthy if no row exists).
    /// </summary>
    Task<AutomationHealthState> GetHealthAsync(Guid automationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets health states for the given automations, keyed by id (Healthy for any with no row).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, AutomationHealthState>> GetHealthAsync(
        IReadOnlyCollection<Guid> automationIds,
        CancellationToken cancellationToken = default);
}
