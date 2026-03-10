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
}
