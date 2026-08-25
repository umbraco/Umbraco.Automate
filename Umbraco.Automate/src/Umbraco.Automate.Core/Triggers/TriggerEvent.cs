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
    /// Gets an optional target automation. When set, the event runs exactly this automation
    /// rather than fanning out to every published automation subscribed to
    /// <see cref="TriggerAlias"/>. Set by entry points that already know which automation the
    /// event belongs to: the webhook endpoint (addressed by automation ID) and the scheduled
    /// trigger job (which evaluates each automation's CRON individually). <c>null</c> for genuine
    /// pub/sub triggers — content and entity events — that should dispatch to all subscribers.
    /// </summary>
    public Guid? TargetAutomationId { get; init; }

    /// <summary>
    /// Gets an optional idempotency key. When set, the outbox will silently drop
    /// duplicate messages with the same topic and key.
    /// Triggers should generate deterministic keys based on event identity
    /// (e.g. "{triggerAlias}:{entityKey}:{eventTimestamp}").
    /// </summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>
    /// Gets the run ID of the automation run that produced this event as a side effect,
    /// or <c>null</c> when the event was raised outside an automation (user save, scheduled
    /// trigger, external webhook, etc.). Stamped by the dispatch path from the ambient
    /// <see cref="Execution.IAutomationOriginAccessor"/>.
    /// </summary>
    public Guid? OriginRunId { get; set; }

    /// <summary>
    /// Gets the automation cascade chain that produced this event — ordered list of
    /// automation IDs (oldest to newest) including the most recent automation as the last
    /// entry. Empty for events raised outside an automation. Receivers detect cycles by
    /// checking whether their own automation ID appears in this chain.
    /// </summary>
    public IReadOnlyList<Guid> OriginAutomationChain { get; set; } = [];
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
