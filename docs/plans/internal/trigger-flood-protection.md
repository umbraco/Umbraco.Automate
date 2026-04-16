# Trigger-Level Flood Protection & Debounce

## Context

When a bulk CMS operation occurs (e.g., publishing 500 content nodes via an import, or a script using the Content Service API), the `ContentPublishedTrigger` fires once per content item. Each fire enqueues a message to the outbox, which dispatches to matching automations, spawning up to 500 concurrent runs.

Today the only guards against this are:
- **Idempotency keys** — prevent the exact same CMS notification from firing twice (same content key + version ID), but each distinct publish is a legitimately different event
- **Per-automation rate limiting** — `RateLimitService` rejects runs exceeding `MaxRunsPerAutomationPerMinute` (default 60) or `MaxConcurrentRunsPerAutomation` (default 10), but rejected events are lost, not queued
- **Outbox backpressure** — `MaxPendingMessages` (default 10,000) rejects new publishes when the outbox is full, but this is a last-resort circuit breaker, not a throttling mechanism

None of these provide deliberate flood protection — the ability to detect a burst of trigger events and handle them gracefully (queue, batch, debounce, or drip-feed) rather than either running them all at once or dropping them.

### Priority note

Lars Skjold Iversen noted this is lower priority for Umbraco.Automate than for Zapier, since we don't charge per-task. The main risk is performance degradation during bulk operations, not cost overruns. This spec captures the design for when we choose to implement it.

---

## Trigger dispatch flow (current)

```
CMS Notification (e.g., ContentPublishedNotification with 500 entities)
  → TriggerNotificationHandler<T>
    → for each entity: trigger.MapEvent(notification) yields N TriggerEvents
      → ITriggerDispatcher.DispatchAsync() per event
        → OutboxTriggerDispatcher enqueues to IOutbox
          → OutboxDispatcher polls, claims, invokes TriggerEventHandler
            → TriggerEventHandler finds matching automations, calls IAutomationExecutor per match
```

The key observation: `ContentPublishedTrigger.MapEvent()` yields one event per published entity. A bulk publish of 500 items produces 500 outbox messages in a tight loop within a single `TriggerNotificationHandler.HandleAsync()` call. Each message independently triggers matching automations.

---

## Design

### Approach: Trigger-level throttle with configurable strategy

Add a throttling layer between `TriggerNotificationHandler` and `ITriggerDispatcher` that intercepts trigger events and applies a configurable strategy before they reach the outbox. This keeps the outbox clean and avoids wasting I/O on events that will ultimately be rate-limited at the execution layer.

### Phase 1: Trigger throttle infrastructure

#### 1.1 New options: `TriggerThrottleOptions`

Bound to `Umbraco:Automate:TriggerThrottle` in appsettings:

```csharp
public sealed class TriggerThrottleOptions
{
    /// <summary>
    /// Whether trigger throttling is enabled globally.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Default strategy applied to all triggers unless overridden.
    /// </summary>
    public ThrottleStrategy DefaultStrategy { get; set; } = ThrottleStrategy.None;

    /// <summary>
    /// Maximum number of trigger events dispatched per trigger alias per minute.
    /// Only applies when strategy includes rate limiting.
    /// 0 = unlimited.
    /// </summary>
    public int DefaultMaxEventsPerMinute { get; set; } = 0;

    /// <summary>
    /// Debounce window for trigger events. When multiple events for the same
    /// trigger + content key arrive within this window, only the last one is dispatched.
    /// Only applies when strategy includes debounce.
    /// </summary>
    public TimeSpan DefaultDebounceWindow { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Per-trigger overrides keyed by trigger alias.
    /// </summary>
    public Dictionary<string, TriggerThrottleOverride> Triggers { get; set; } = [];
}

public sealed class TriggerThrottleOverride
{
    public ThrottleStrategy? Strategy { get; set; }
    public int? MaxEventsPerMinute { get; set; }
    public TimeSpan? DebounceWindow { get; set; }
}

public enum ThrottleStrategy
{
    /// <summary>No throttling — all events dispatched immediately.</summary>
    None = 0,

    /// <summary>Rate limit: dispatch up to N events per minute, queue the rest.</summary>
    RateLimit = 1,

    /// <summary>Debounce: coalesce rapid events for the same entity, dispatch only the last.</summary>
    Debounce = 2,

    /// <summary>Both rate limiting and debounce applied.</summary>
    RateLimitAndDebounce = 3,
}
```

#### 1.2 New service: `ITriggerThrottle`

Sits between the notification handler and the dispatcher:

```csharp
public interface ITriggerThrottle
{
    /// <summary>
    /// Submits a trigger event for throttled dispatch.
    /// May dispatch immediately, queue for later, or coalesce with other events.
    /// </summary>
    Task SubmitAsync(TriggerEvent triggerEvent, CancellationToken cancellationToken);
}
```

#### 1.3 Implementation: `TriggerThrottle`

The throttle maintains in-memory state per trigger alias:

**Rate limiting:**
- Uses `System.Threading.RateLimiting.FixedWindowRateLimiter` per trigger alias (same library as the webhook rate limiter)
- Events that exceed the limit are queued in a bounded in-memory buffer
- A background task drains the buffer as permits become available
- If the buffer is full, the event is dropped and logged as a metric (`automate.triggers.throttled`)

**Debounce:**
- Maintains a dictionary of `(triggerAlias, entityKey) → (TriggerEvent, Timer)` entries
- When an event arrives, if an existing entry exists for the same entity key, the timer is reset and the event is replaced with the newer one
- When the timer expires (after `DebounceWindow`), the event is dispatched
- The entity key is extracted from the trigger event's `IdempotencyKey` — the content key portion identifies the entity

```csharp
internal sealed class TriggerThrottle : ITriggerThrottle, IDisposable
{
    private readonly ITriggerDispatcher _dispatcher;
    private readonly IOptions<TriggerThrottleOptions> _options;
    private readonly ConcurrentDictionary<string, RateLimiter> _rateLimiters = new();
    private readonly ConcurrentDictionary<string, DebounceEntry> _debounceEntries = new();

    // Rate limiting
    private RateLimiter GetOrCreateLimiter(string triggerAlias, int maxPerMinute)
        => _rateLimiters.GetOrAdd(triggerAlias, _ =>
            new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = maxPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = maxPerMinute * 2, // buffer up to 2x the limit
            }));

    // Debounce
    private record DebounceEntry(TriggerEvent Event, CancellationTokenSource Cts);
}
```

#### 1.4 Integration: `TriggerNotificationHandler` change

Replace the direct `ITriggerDispatcher.DispatchAsync()` call with `ITriggerThrottle.SubmitAsync()`:

```csharp
// Before:
await dispatcher.DispatchAsync(evt, cancellationToken);

// After:
await triggerThrottle.SubmitAsync(evt, cancellationToken);
```

When throttling is disabled (`Enabled = false`), the `ITriggerThrottle` implementation passes through directly to `ITriggerDispatcher` with no overhead.

#### 1.5 Metrics

New OpenTelemetry counters in `AutomateMetrics`:
- `automate.triggers.throttled` — events delayed by rate limiting (tagged by trigger alias)
- `automate.triggers.debounced` — events coalesced by debounce (tagged by trigger alias)
- `automate.triggers.dropped` — events dropped because the throttle buffer was full (tagged by trigger alias)

#### 1.6 Health check

New `TriggerThrottleHealthCheck`:
- Warning when any trigger's throttle buffer is >80% full
- Error when any trigger's throttle buffer is full and events are being dropped

### Phase 2: Per-automation throttle overrides

Allow automation creators to configure throttle settings on their automation's trigger, overriding the global defaults. This gives users control without requiring server-level config:

```csharp
// New fields on TriggerConfiguration (stored in automation definition)
public ThrottleStrategy? ThrottleStrategy { get; set; }
public int? MaxEventsPerMinute { get; set; }
public TimeSpan? DebounceWindow { get; set; }
```

Resolution order: automation trigger settings → per-trigger appsettings override → global default.

### Phase 3: Batching support (future)

A more advanced strategy where multiple trigger events are grouped into a single run with a collection input, rather than spawning one run per event. This requires:
- A `BatchWindow` option (e.g., "collect events for 5 seconds, then dispatch as one batch")
- A batch-aware trigger output model (list of entities rather than single entity)
- ForEach support in the workflow engine to iterate over batch items

This depends on ForEach implementation (see `workflowcore-feature-gaps.md`) and is out of scope for the initial flood protection feature.

---

## Design considerations

### Why throttle before the outbox, not after?

Throttling at the outbox consumer (`TriggerEventHandler`) level would still write all events to the database. For a 500-item bulk publish, that's 500 outbox rows written, 500 rows claimed, 500 handler invocations, and then the handler decides to drop or delay. Throttling before the outbox avoids all that I/O.

The tradeoff is that in-memory throttle state is lost on process restart. This is acceptable because:
- Trigger events are best-effort — a missed event during a restart is a known limitation
- The debounce window is short (seconds), so the blast radius is small
- Rate limit counters reset naturally on restart

### Why not just increase the per-automation rate limit?

The per-automation rate limit (`RateLimitService`) operates at the execution layer — the event has already been dispatched, the outbox row written, the handler invoked, and the automation resolved. Rejecting at that point wastes all the preceding work. It also **drops** the event rather than queuing it. The trigger throttle operates earlier and can queue/coalesce rather than reject.

### Debounce entity key extraction

The debounce needs a "same entity" grouping key. For content triggers, this is the content key (GUID). The `IdempotencyKey` format is `{alias}:{contentKey}:v{versionId}` — we extract the `{contentKey}` segment. For triggers without content keys (e.g., webhook triggers), debounce doesn't apply (each event is unique). The `ITriggerThrottle` implementation can check whether the trigger event has an extractable entity key and skip debounce if not.

### Thread safety

The throttle is a singleton service processing events from multiple notification handlers concurrently. `ConcurrentDictionary` for limiter/debounce state, and `RateLimiter` is thread-safe by design. Debounce timer management needs care — use `CancellationTokenSource` swap pattern rather than `System.Threading.Timer` to avoid race conditions.

---

## Migration

No database changes — throttle state is entirely in-memory. Configuration via appsettings.

---

## Testing

### Unit tests

- `TriggerThrottleTests`:
  - Events pass through when throttling disabled
  - Rate limiter permits up to MaxEventsPerMinute then queues
  - Queued events drain when permits renew
  - Buffer full drops events and increments metric
  - Debounce coalesces rapid events for same entity key
  - Debounce dispatches after window expires
  - Debounce resets timer on new event within window
  - Per-trigger overrides take precedence over defaults
  - Events without entity key skip debounce

### Integration tests

- Bulk content publish (simulated) with throttle enabled — verify only N events dispatched per minute
- Debounce window — publish same content 5 times in rapid succession, verify only 1 run starts

---

## Open questions

1. **Should dropped events be dead-lettered somewhere?** Currently they'd only show up in metrics/logs. A dead-letter table adds recovery options but also adds I/O — which is what we're trying to avoid. Recommend metrics-only for Phase 1.
2. **Should the throttle buffer be bounded per-trigger or globally?** Per-trigger is more predictable (one noisy trigger doesn't starve others). Recommend per-trigger with a configurable buffer size.
3. **What happens to in-flight debounce entries on graceful shutdown?** Flush them — dispatch all pending debounce entries immediately rather than losing them. Add a `DrainAsync()` method called during `IHostedService.StopAsync()`.
4. **Should webhook triggers be throttled?** The new HTTP-level rate limiting (PR #24) handles inbound request rate. The trigger throttle is more relevant for CMS notification triggers that can fire in bulk from internal operations. Webhook triggers could opt out by default.
5. **Default state: enabled or disabled?** Recommend disabled by default (`Enabled = false`) since this is a lower-priority feature and we don't want to change behavior for existing installations. Users opt in when they need it.
