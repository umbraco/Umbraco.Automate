namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Convenience base class for triggers that respond to incoming HTTP webhooks.
/// Combines <see cref="TriggerBase{TSettings,TOutput}"/> metadata with
/// <see cref="IWebhookTrigger"/> activation.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type.</typeparam>
/// <typeparam name="TOutput">The output POCO type.</typeparam>
public abstract class WebhookTriggerBase<TSettings, TOutput>
    : TriggerBase<TSettings, TOutput>, IWebhookTrigger
    where TSettings : class, new()
    where TOutput : class
{
    /// <inheritdoc />
    public abstract IEnumerable<string> AllowedMethods { get; }
}
