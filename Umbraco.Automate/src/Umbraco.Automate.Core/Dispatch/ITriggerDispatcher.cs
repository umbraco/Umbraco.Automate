using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Core.Dispatch;

/// <summary>
/// Dispatches trigger events to matching automations.
/// The default implementation publishes to the outbox for reliable async dispatch.
/// </summary>
public interface ITriggerDispatcher
{
    /// <summary>
    /// Dispatches a trigger event for processing.
    /// </summary>
    /// <param name="triggerEvent">The trigger event to dispatch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DispatchAsync(TriggerEvent triggerEvent, CancellationToken cancellationToken);
}
