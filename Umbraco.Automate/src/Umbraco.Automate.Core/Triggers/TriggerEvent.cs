namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Represents an event fired by a trigger, ready for dispatch to matching automations.
/// </summary>
public class TriggerEvent
{
    /// <summary>
    /// Gets the alias of the trigger that produced this event.
    /// </summary>
    public required string TriggerAlias { get; init; }

    /// <summary>
    /// Gets the type of initiator. Use <see cref="TriggerInitiatorType"/> constants.
    /// </summary>
    public required string InitiatorType { get; init; }

    /// <summary>
    /// Gets an optional identifier for the initiator (e.g. user key, webhook ID).
    /// </summary>
    public string? InitiatorId { get; init; }

    /// <summary>
    /// Gets an optional idempotency key. When set, the outbox will silently drop
    /// duplicate messages with the same topic and key.
    /// Triggers should generate deterministic keys based on event identity
    /// (e.g. "{triggerAlias}:{entityKey}:{eventTimestamp}").
    /// </summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// Internal interface for pattern matching in the dispatcher to extract typed output.
/// </summary>
internal interface ITriggerEventWithOutput
{
    /// <summary>
    /// Gets the output object.
    /// </summary>
    object GetOutput();
}

/// <summary>
/// A strongly-typed trigger event with output data.
/// </summary>
/// <typeparam name="TOutput">The output type produced by the trigger.</typeparam>
public class TriggerEvent<TOutput> : TriggerEvent, ITriggerEventWithOutput
    where TOutput : class
{
    /// <summary>
    /// Gets the typed output data produced by the trigger.
    /// </summary>
    public required TOutput Output { get; init; }

    /// <inheritdoc />
    object ITriggerEventWithOutput.GetOutput() => Output;
}
