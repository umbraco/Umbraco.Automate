using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Dispatch;

/// <summary>
/// Generic notification handler that bridges Umbraco notifications to trigger dispatch.
/// One instance is auto-registered per distinct notification type discovered at startup.
/// </summary>
/// <remarks>
/// No server-role filtering here — Umbraco notifications fire in-process on the node that
/// handles the request and do not participate in distributed cache. Every node must capture
/// the event and dispatch it to the message bus. Deduplication is the responsibility of
/// the consumer / execution layer.
/// </remarks>
/// <typeparam name="TNotification">The Umbraco notification type.</typeparam>
internal sealed class TriggerNotificationHandler<TNotification>(
    IEnumerable<INotificationTrigger<TNotification>> triggers,
    ITriggerDispatcher dispatcher,
    ITriggerSubscriptionRegistry subscriptionRegistry,
    IAutomationOriginAccessor originAccessor,
    IRuntimeState runtimeState,
    ILogger<TriggerNotificationHandler<TNotification>> logger) : INotificationAsyncHandler<TNotification>
    where TNotification : INotification
{
    /// <inheritdoc />
    public async Task HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        // Skip dispatch during install/upgrade — Automate tables may not exist yet.
        if (runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        // Read once: if this notification is firing as a side effect of an action, the
        // origin accessor carries the run that caused it via AsyncLocal. We stamp every
        // dispatched event with that origin so downstream automations can filter loops
        // and the chain-depth backstop has something to count.
        var origin = originAccessor.Current;

        foreach (var trigger in triggers)
        {
            // Skip triggers that no published+enabled automation subscribes to. Without
            // this check we'd write an outbox row per CMS notification (contentSaved,
            // contentBatchSaved, contentBatchPublished, …) only for the consumer to find
            // no match and drop it — wasted I/O and outbox churn.
            //
            // All trigger implementations also implement ITrigger (via TriggerBase) so the
            // cast is safe; if a custom trigger ever bypasses TriggerBase, fall through and
            // dispatch as before rather than refusing to fire.
            if (trigger is IStepType stepType
                && !await subscriptionRegistry.HasSubscribersAsync(stepType.Alias, cancellationToken))
            {
                logger.LogDebug(
                    "Skipping trigger {TriggerAlias} — no published, enabled automations are subscribed",
                    stepType.Alias);
                continue;
            }

            foreach (var evt in trigger.MapEvent(notification))
            {
                if (origin is not null)
                {
                    evt.OriginRunId = origin.RunId;
                    evt.OriginAutomationId = origin.AutomationId;
                    evt.ChainDepth = origin.ChainDepth + 1;
                }

                await dispatcher.DispatchAsync(evt, cancellationToken);
            }
        }
    }
}
