namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Activation interface declaring that a trigger responds to events of type <typeparamref name="TEvent"/>.
/// The trigger maps the event to zero or more <see cref="TriggerEvent"/> instances for dispatch.
/// </summary>
/// <typeparam name="TEvent">The event type this trigger responds to.</typeparam>
public interface IEventTrigger<in TEvent>
{
    /// <summary>
    /// Maps an event to zero or more trigger events.
    /// Return an empty sequence if this event does not match the trigger's criteria.
    /// </summary>
    /// <param name="event">The event to map.</param>
    /// <returns>Zero or more trigger events to dispatch.</returns>
    IEnumerable<TriggerEvent> MapEvent(TEvent @event);
}
