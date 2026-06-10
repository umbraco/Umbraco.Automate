namespace Umbraco.Automate.Core.Messaging;

/// <summary>
/// Publishes messages to the outbox for reliable async dispatch.
/// </summary>
internal interface IOutbox
{
    /// <summary>
    /// Publishes a message to the specified topic.
    /// </summary>
    /// <param name="topic">The topic name for routing.</param>
    /// <param name="message">The message payload (will be JSON-serialized).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="idempotencyKey">Optional idempotency key. When set, duplicate messages
    /// with the same topic and key are silently dropped.</param>
    Task PublishAsync(string topic, object message, CancellationToken cancellationToken, string? idempotencyKey = null);
}
