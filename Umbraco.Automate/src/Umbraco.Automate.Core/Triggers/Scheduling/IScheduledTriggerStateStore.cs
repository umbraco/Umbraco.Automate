namespace Umbraco.Automate.Core.Triggers.Scheduling;

/// <summary>
/// Persists last-fired timestamps for scheduled triggers so that schedules survive restarts.
/// </summary>
public interface IScheduledTriggerStateStore
{
    /// <summary>
    /// Gets the last-fired time for a given automation, or null if never fired.
    /// </summary>
    Task<DateTime?> GetLastFiredAsync(Guid automationId, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the last-fired time for a given automation.
    /// </summary>
    Task SetLastFiredAsync(Guid automationId, DateTime firedUtc, CancellationToken cancellationToken);
}
