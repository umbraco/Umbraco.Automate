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

    /// <summary>
    /// Returns <c>true</c> if this node can currently process messages for this handler's topic.
    /// The dispatcher checks this each poll and excludes ineligible topics from the claim attempt
    /// — leaving messages in <c>Pending</c> for an eligible node (or this node, on a later poll
    /// once eligibility settles).
    /// </summary>
    /// <remarks>
    /// Default implementation returns <c>true</c>. Override when the handler has node-level
    /// eligibility constraints (e.g. server role, feature flag).
    /// </remarks>
    bool CanProcessNow() => true;
}
