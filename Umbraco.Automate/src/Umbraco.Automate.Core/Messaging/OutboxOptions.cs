namespace Umbraco.Automate.Core.Messaging;

/// <summary>
/// Configuration options for the outbox message dispatcher.
/// Bound to <c>Umbraco:Automate:Outbox</c> in appsettings.json.
/// </summary>
public sealed class OutboxOptions
{
    /// <summary>
    /// How often the dispatcher polls for new messages.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Maximum number of delivery attempts before dead-lettering.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Base delay for exponential backoff between retries.
    /// Actual delay = BaseRetryDelay * 2^retryCount.
    /// </summary>
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a claimed message can be held before it's considered stale
    /// and released for another instance to pick up (handles process crashes).
    /// </summary>
    public TimeSpan StaleClaimThreshold { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long to keep completed messages before cleanup removes them.
    /// </summary>
    public TimeSpan CompletedRetention { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Grace period during shutdown: the dispatcher stops claiming new messages
    /// but waits up to this duration for any in-flight message to finish processing.
    /// </summary>
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of pending messages allowed in the outbox.
    /// When the limit is reached, new publish calls are rejected with <see cref="OutboxBackpressureException"/>.
    /// Set to 0 to disable the limit.
    /// </summary>
    public int MaxPendingMessages { get; set; } = 10_000;
}
