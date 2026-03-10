namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Activation interface for triggers that run on a CRON schedule.
/// Only evaluated on the SchedulingPublisher node in load-balanced environments.
/// </summary>
public interface IScheduledTrigger
{
    /// <summary>
    /// Gets the CRON expression for this trigger, optionally derived from the trigger settings.
    /// </summary>
    /// <param name="settings">The deserialized trigger settings, or null.</param>
    /// <returns>A CRON expression string.</returns>
    string GetCronExpression(object? settings);
}
