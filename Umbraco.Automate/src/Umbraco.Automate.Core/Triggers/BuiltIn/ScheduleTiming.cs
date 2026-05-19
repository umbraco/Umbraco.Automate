namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Controls how strictly a <see cref="ScheduledTrigger"/> honours its CRON tick.
/// </summary>
public enum ScheduleTiming
{
    /// <summary>
    /// Fires around the scheduled time, with a deterministic per-automation offset within
    /// the configured <c>MaxJitter</c> window. Spreads load when many automations share a tick.
    /// </summary>
    Flexible = 0,

    /// <summary>
    /// Fires exactly on the scheduled tick with no jitter applied.
    /// </summary>
    Precise = 1,
}
