namespace Umbraco.Automate.Persistence.Triggers;

/// <summary>
/// EF Core entity for the <c>umbracoAutomateScheduledTriggerState</c> table.
/// Tracks last-fired times for scheduled triggers.
/// </summary>
internal sealed class ScheduledTriggerStateEntity
{
    public Guid AutomationId { get; set; }

    public DateTime LastFiredUtc { get; set; }
}
