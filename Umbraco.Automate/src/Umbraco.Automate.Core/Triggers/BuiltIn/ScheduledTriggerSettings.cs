using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Settings for the <see cref="ScheduledTrigger"/>.
/// </summary>
public sealed class ScheduledTriggerSettings
{
    /// <summary>
    /// Gets or sets the CRON expression that determines the schedule (e.g. "0 */5 * * *" for every 5 minutes).
    /// Supports standard 5-field CRON syntax.
    /// </summary>
    [Field(Label = "CRON Expression", Description = "A CRON expression defining the schedule (e.g. '0 9 * * 1-5' for weekdays at 9am).")]
    public string CronExpression { get; set; } = "0 * * * *";

    /// <summary>
    /// Gets or sets the IANA time zone ID for evaluating the CRON expression (e.g. "Europe/London").
    /// Defaults to UTC if not specified.
    /// </summary>
    [Field(Label = "Time Zone", Description = "IANA time zone ID (e.g. 'Europe/London'). Defaults to UTC.", SortOrder = 1)]
    public string? TimeZone { get; set; }
}
