namespace Umbraco.Automate.Persistence.Transport;

/// <summary>
/// EF Core entity representing a message in the database-backed CAP transport queue.
/// Messages are written by the transport and polled by consumer clients.
/// </summary>
internal sealed class TransportMessageEntity
{
    public long Id { get; set; }

    /// <summary>
    /// The CAP topic name (e.g. "umbraco.automate.trigger").
    /// </summary>
    public required string Topic { get; set; }

    /// <summary>
    /// JSON-serialized CAP message headers.
    /// </summary>
    public required string Headers { get; set; }

    /// <summary>
    /// The message body (serialized payload).
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// When the message was enqueued.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The consumer group that claimed this message, or null if unclaimed.
    /// </summary>
    public string? ClaimedByGroup { get; set; }

    /// <summary>
    /// When the message was claimed by a consumer, or null if unclaimed.
    /// </summary>
    public DateTime? ClaimedUtc { get; set; }
}
