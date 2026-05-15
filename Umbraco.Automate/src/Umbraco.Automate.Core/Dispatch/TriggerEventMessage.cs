namespace Umbraco.Automate.Core.Dispatch;

/// <summary>
/// Serializable message published to the outbox when a trigger fires.
/// </summary>
internal sealed class TriggerEventMessage
{
    /// <summary>
    /// Gets or sets the alias of the trigger that fired.
    /// </summary>
    public required string TriggerAlias { get; set; }

    /// <summary>
    /// Gets or sets the initiator type ("system", "user", "webhook", "ai-agent").
    /// </summary>
    public required string InitiatorType { get; set; }

    /// <summary>
    /// Gets or sets an optional initiator identifier.
    /// </summary>
    public string? InitiatorId { get; set; }

    /// <summary>
    /// Gets or sets the JSON-serialized trigger output data.
    /// </summary>
    public string? OutputData { get; set; }

    /// <summary>
    /// Gets or sets the assembly-qualified type name of the output data.
    /// </summary>
    public string? OutputTypeName { get; set; }

    /// <summary>
    /// Gets or sets an optional idempotency key for duplicate prevention.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the run ID of the automation that produced this event as a side
    /// effect, or <c>null</c> when raised from outside an automation. Used in diagnostic
    /// logging only — cycle detection uses <see cref="OriginAutomationChain"/>.
    /// </summary>
    public Guid? OriginRunId { get; set; }

    /// <summary>
    /// Gets or sets the automation cascade chain — ordered list of automation IDs (oldest
    /// to newest) including the most recent. Receivers skip events where their own ID
    /// appears in this chain (cycle detection). Length doubles as the cascade depth and
    /// is bounded by <c>ExecutionOptions.MaxChainDepth</c>.
    /// </summary>
    public IReadOnlyList<Guid> OriginAutomationChain { get; set; } = [];
}
