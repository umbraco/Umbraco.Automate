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
    [Field]
    public string CronExpression { get; set; } = "0 * * * *";

    /// <summary>
    /// Gets or sets the IANA time zone ID for evaluating the CRON expression (e.g. "Europe/London").
    /// Defaults to UTC if not specified.
    /// </summary>
    [Field(SortOrder = 1)]
    public string? TimeZone { get; set; }

    /// <summary>
    /// Gets or sets the timing strictness. Stored as a string so the dropdown picker
    /// round-trips cleanly — parsed into <see cref="ScheduleTiming"/> at evaluation time.
    /// </summary>
    [Field(
        EditorUiAlias = "Umb.PropertyEditorUi.Dropdown",
        EditorConfig = """[{ "alias": "items", "value": ["Flexible", "Precise"] }]""",
        SortOrder = 2)]
    public string Timing { get; set; } = nameof(ScheduleTiming.Flexible);
}
