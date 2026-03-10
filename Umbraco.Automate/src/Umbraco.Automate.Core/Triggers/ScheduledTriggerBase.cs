namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Convenience base class for triggers that run on a CRON schedule.
/// Combines <see cref="TriggerBase{TSettings,TOutput}"/> metadata with
/// <see cref="IScheduledTrigger"/> activation.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type.</typeparam>
/// <typeparam name="TOutput">The output POCO type.</typeparam>
public abstract class ScheduledTriggerBase<TSettings, TOutput>
    : TriggerBase<TSettings, TOutput>, IScheduledTrigger
    where TSettings : class, new()
    where TOutput : class
{
    /// <inheritdoc />
    public abstract string GetCronExpression(object? settings);
}
