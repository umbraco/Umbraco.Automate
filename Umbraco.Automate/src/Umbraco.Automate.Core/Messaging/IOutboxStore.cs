namespace Umbraco.Automate.Core.Messaging;

/// <summary>
/// Persistence abstraction for the outbox message store.
/// </summary>
internal interface IOutboxStore
{
    /// <summary>
    /// Inserts a new message into the outbox.
    /// </summary>
    Task InsertAsync(OutboxMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Claims the next available message for any of the given topics.
    /// Uses optimistic concurrency — only one instance wins the claim.
    /// </summary>
    /// <param name="topics">Topics to claim from.</param>
    /// <param name="instanceId">Unique identifier for this instance.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The claimed message, or null if none available.</returns>
    Task<OutboxMessage?> ClaimNextAsync(IReadOnlyList<string> topics, string instanceId, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a message as successfully processed.
    /// </summary>
    Task MarkCompletedAsync(long messageId, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a message as failed, scheduling a retry if applicable.
    /// </summary>
    Task MarkFailedAsync(long messageId, string error, DateTime? nextRetryUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Releases a claimed message back to <c>Pending</c> without incrementing the retry count.
    /// Used when the handler determines it can't process the message right now (e.g. node
    /// became ineligible between claim and handle) and another node — or this node on a later
    /// poll — should be allowed to claim it indefinitely.
    /// </summary>
    Task ReleaseClaimAsync(long messageId, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a message as dead-lettered (no more retries).
    /// </summary>
    Task MarkDeadLetteredAsync(long messageId, string error, CancellationToken cancellationToken);

    /// <summary>
    /// Removes completed messages older than the specified age.
    /// </summary>
    Task CleanupCompletedAsync(TimeSpan olderThan, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the count of messages grouped by status.
    /// </summary>
    Task<OutboxStats> GetStatsAsync(CancellationToken cancellationToken);
}
