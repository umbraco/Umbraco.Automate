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
public sealed class ScheduledTrigger : ScheduledTriggerBase<ScheduledTriggerSettings, ScheduledTriggerOutput>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduledTrigger"/> class.
    /// </summary>
    public ScheduledTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

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
}
