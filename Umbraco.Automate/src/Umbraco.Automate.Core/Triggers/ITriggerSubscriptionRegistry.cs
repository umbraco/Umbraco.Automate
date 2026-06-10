namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Tracks which trigger aliases have at least one published, enabled automation subscribed
/// to them, so the notification dispatcher can skip producing outbox messages that nobody
/// will consume.
/// </summary>
/// <remarks>
/// The registry is best-effort: the consumer side (<c>TriggerEventHandler</c>) still
/// performs the authoritative check against the current automation state. If the cache is
/// stale or not yet warm, this method may return <c>true</c> for an alias that has no
/// subscribers — the worst outcome is one wasted outbox round-trip, not a missed event.
/// </remarks>
public interface ITriggerSubscriptionRegistry
{
    /// <summary>
    /// Returns <c>true</c> if any published, enabled automation subscribes to the given
    /// trigger alias. Returns <c>true</c> defensively when the registry can't yet answer
    /// (e.g. before migrations have completed) so events are not silently dropped.
    /// </summary>
    Task<bool> HasSubscribersAsync(string triggerAlias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards the cached subscriber set so the next call to
    /// <see cref="HasSubscribersAsync"/> rebuilds it from the automation store.
    /// Called by the notification handlers when an automation lifecycle event occurs.
    /// </summary>
    void Invalidate();
}
