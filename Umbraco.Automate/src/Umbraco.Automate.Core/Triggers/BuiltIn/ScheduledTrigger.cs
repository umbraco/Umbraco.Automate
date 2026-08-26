namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// A trigger that fires on a configurable CRON schedule.
/// The CRON expression is evaluated by the scheduling infrastructure;
/// this trigger provides the settings and output contract.
/// </summary>
[Trigger("umbracoAutomate.scheduled", "Scheduled Trigger",
    Description = "Fires on a configurable CRON schedule.",
    Group = "Core",
    Icon = "icon-time")]
public sealed class ScheduledTrigger : ScheduledTriggerBase<ScheduledTriggerSettings, ScheduledTriggerOutput>, ISupportsManualRun
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduledTrigger"/> class.
    /// </summary>
    public ScheduledTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// An on-demand run is the scheduled run brought forward, so there is no payload to stand
    /// in for — the schedule itself is the only thing being skipped.
    /// </remarks>
    public ManualRunOutput CreateManualRunOutput(object? settings) => ManualRunOutput.None;

    /// <inheritdoc />
    public override string GetCronExpression(object? settings)
    {
        if (settings is ScheduledTriggerSettings typedSettings
            && !string.IsNullOrWhiteSpace(typedSettings.CronExpression))
        {
            return typedSettings.CronExpression;
        }

        return "0 * * * *"; // Default: every hour
    }

    /// <inheritdoc />
    public override TimeZoneInfo GetTimeZone(object? settings)
    {
        if (settings is not ScheduledTriggerSettings typedSettings
            || string.IsNullOrWhiteSpace(typedSettings.TimeZone))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(typedSettings.TimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
