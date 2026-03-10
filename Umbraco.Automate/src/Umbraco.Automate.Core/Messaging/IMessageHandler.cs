namespace Umbraco.Automate.Core.Messaging;

/// <summary>
/// Handles outbox messages for a specific topic.
/// Implementations are registered in DI and discovered by <see cref="OutboxDispatcher"/>.
/// </summary>
internal interface IMessageHandler
{
    /// <summary>
    /// The topic this handler processes.
    /// </summary>
    string Topic { get; }

    /// <summary>
    /// Handles a claimed outbox message.
    /// </summary>
    /// <param name="body">The JSON-serialized message body.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task HandleAsync(string body, CancellationToken cancellationToken);
}
