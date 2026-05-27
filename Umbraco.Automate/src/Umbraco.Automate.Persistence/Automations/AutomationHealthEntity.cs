namespace Umbraco.Automate.Persistence.Automations;

/// <summary>
/// EF Core entity for the <c>umbracoAutomateAutomationHealth</c> table.
/// Per-automation circuit-breaker health state. An automation with no row is implicitly Healthy.
/// </summary>
internal sealed class AutomationHealthEntity
{
    public Guid AutomationId { get; set; }

    public int Health { get; set; }

    public DateTime? WarningIssuedUtc { get; set; }

    public DateTime? DisabledUtc { get; set; }
}
