using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output data produced by the <see cref="ScheduledTrigger"/>.
/// </summary>
public sealed class ScheduledTriggerOutput
{
    /// <summary>
    /// Gets or sets the UTC timestamp when the trigger fired.
    /// </summary>
    [Description("The UTC timestamp when the trigger fired.")]
    public DateTime FiredAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the CRON expression that was evaluated.
    /// </summary>
    [Description("The CRON expression that was evaluated.")]
    public string CronExpression { get; set; } = string.Empty;
}
