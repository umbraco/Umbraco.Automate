namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Per-automation circuit-breaker health state, persisted in its own table
/// (<c>umbracoAutomateAutomationHealth</c>) so runtime health stays off the versioned
/// <see cref="Automation"/> definition. An automation with no row is implicitly
/// <see cref="AutomationHealth.Healthy"/>.
/// </summary>
public sealed class AutomationHealthState
{
    /// <summary>
    /// Gets or sets the automation this health state belongs to.
    /// </summary>
    public Guid AutomationId { get; set; }

    /// <summary>
    /// Gets or sets the current health.
    /// </summary>
    public AutomationHealth Health { get; set; } = AutomationHealth.Healthy;

    /// <summary>
    /// Gets or sets when the most recent degradation warning was issued (the grace-period clock),
    /// or null if no warning is active.
    /// </summary>
    public DateTime? WarningIssuedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the automation was auto-disabled, or null if not disabled.
    /// </summary>
    public DateTime? DisabledUtc { get; set; }
}
