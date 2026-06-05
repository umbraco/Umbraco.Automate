namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// The circuit-breaker health of an automation. This is a separate axis from
/// <see cref="AutomationStatus"/> (the publish lifecycle): the breaker owns health and never
/// touches status. An automation runs only when it is both <see cref="AutomationStatus.Published"/>
/// and the circuit is closed (<see cref="Healthy"/> or <see cref="Degraded"/>).
/// </summary>
public enum AutomationHealth
{
    /// <summary>
    /// Operating normally.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// A degradation warning has been issued (the error rate is climbing) but the automation
    /// still runs. Within the grace period before auto-disable can trigger.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// Auto-disabled by the circuit breaker. Automatic triggers no longer run; the publish
    /// status is unchanged. Cleared only by an explicit re-enable or a re-publish.
    /// </summary>
    Disabled = 2,
}
