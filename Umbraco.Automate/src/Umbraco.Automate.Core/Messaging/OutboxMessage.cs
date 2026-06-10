namespace Umbraco.Automate.Core.Messaging;

/// <summary>
/// Represents a message in the outbox.
/// </summary>
internal sealed class OutboxMessage
{
    public long Id { get; set; }

    /// <summary>
    /// The topic name for routing (e.g. "umbraco.automate.trigger").
    /// </summary>
    public required string Topic { get; set; }

    /// <summary>
    /// The JSON-serialized message payload.
    /// </summary>
    public required string Body { get; set; }

    /// <summary>
    /// Optional idempotency key. When set, the outbox store enforces uniqueness
    /// per topic+key to prevent duplicate message processing.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Current processing status.
    /// </summary>
    public MessageStatus Status { get; set; }

    /// <summary>
    /// Number of times delivery has been attempted.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// When the next retry should be attempted, or null for immediate.
    /// </summary>
    public DateTime? NextRetryUtc { get; set; }

    /// <summary>
    /// The last error message, if any.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// The instance ID that claimed this message for processing.
    /// Used for optimistic concurrency in multi-instance deployments.
    /// </summary>
    public string? ClaimedByInstance { get; set; }

    /// <summary>
    /// When the message was claimed.
    /// </summary>
    public DateTime? ClaimedUtc { get; set; }
}

/// <summary>
/// Processing status of an outbox message.
/// </summary>
internal enum MessageStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    DeadLettered = 4,
}
