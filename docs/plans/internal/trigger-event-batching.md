# Trigger Event Batching

## Context

Today, CMS notification triggers fire one event per entity. Publishing 500 content nodes spawns 500 outbox messages and up to 500 automation runs. The trigger flood protection spec (`trigger-flood-protection.md`) addresses the symptom — throttling or debouncing the event volume. This spec addresses the root cause: **instead of N runs for N events, collect them into one run with a batch input**.

The infrastructure for this partially exists:
- `ContentBatchPublishedTrigger` already yields a single `TriggerEvent<BatchTriggerOutput<ContentPublishedTriggerOutput>>` containing an `Items` collection and `Count`
- `BatchTriggerOutput<T>` is a generic wrapper for batch trigger outputs
- `AutomationWorkflowData.TriggerOutput` is a `Dictionary<string, object?>` that can hold collections
- The trigger dispatch pipeline serializes/deserializes output generically

What's missing is:
1. A **batching window** — collecting events that arrive within a time window into a single batch event
2. **ForEach control flow** — iterating over the batch items within a single run (spec'd separately in `workflowcore-feature-gaps.md`)
3. **User configuration** — letting automation creators choose between per-item and batched execution

### Dependency

This feature has a hard dependency on **ForEach** control flow implementation. Without ForEach, a batch trigger output is only useful if the action can natively handle a collection (e.g., a bulk API call). For most use cases, users need ForEach to process each item in the batch individually.

The batching window (Phase 1 below) can be implemented independently — it produces batch events that the `ContentBatchPublishedTrigger` already handles. ForEach makes it useful to a wider range of automations.

---

## Current batch trigger flow

```
ContentPublishedNotification (contains N entities)
  → ContentBatchPublishedTrigger.MapEvent()
    → yields 1 TriggerEvent<BatchTriggerOutput<ContentPublishedTriggerOutput>>
      → BatchTriggerOutput.Items = [item1, item2, ..., itemN]
      → BatchTriggerOutput.Count = N
```

This works when the Umbraco notification itself contains the full batch (e.g., a multi-content publish operation). But it doesn't help when events arrive as **separate notifications** over time — a loop publishing content one at a time, or multiple users publishing concurrently.

---

## Design

### Phase 1: Batching window in `TriggerThrottle`

Extend the `TriggerThrottle` (from `trigger-flood-protection.md`) with a `Batch` strategy that collects events within a time window and dispatches them as a single batch event.

#### 1.1 New `ThrottleStrategy` value

```csharp
public enum ThrottleStrategy
{
    None = 0,
    RateLimit = 1,
    Debounce = 2,
    RateLimitAndDebounce = 3,
    Batch = 4,
}
```

#### 1.2 New options on `TriggerThrottleOptions`

```csharp
public sealed class TriggerThrottleOptions
{
    // ... existing options from trigger-flood-protection.md ...

    /// <summary>
    /// Time window for collecting trigger events into a batch.
    /// Events arriving within this window are grouped into a single batch event.
    /// Only applies when strategy is Batch.
    /// </summary>
    public TimeSpan DefaultBatchWindow { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of events in a single batch.
    /// When the limit is reached, the batch is dispatched immediately
    /// regardless of the remaining window time.
    /// </summary>
    public int DefaultMaxBatchSize { get; set; } = 100;
}

public sealed class TriggerThrottleOverride
{
    // ... existing overrides ...
    public TimeSpan? BatchWindow { get; set; }
    public int? MaxBatchSize { get; set; }
}
```

#### 1.3 Batch collector in `TriggerThrottle`

When `Strategy == Batch`, the throttle maintains per-trigger-alias batch buffers:

```csharp
internal sealed class BatchCollector
{
    private readonly string _triggerAlias;
    private readonly TimeSpan _batchWindow;
    private readonly int _maxBatchSize;
    private readonly List<TriggerEvent> _buffer = [];
    private readonly object _lock = new();
    private CancellationTokenSource? _windowCts;
    private Task? _dispatchTask;

    /// <summary>
    /// Adds an event to the current batch.
    /// If this is the first event, starts the batch window timer.
    /// If the batch reaches max size, dispatches immediately.
    /// </summary>
    public async Task AddAsync(TriggerEvent evt, Func<BatchTriggerEvent, Task> dispatchCallback)
    {
        bool shouldDispatch;
        lock (_lock)
        {
            _buffer.Add(evt);
            shouldDispatch = _buffer.Count >= _maxBatchSize;

            if (_buffer.Count == 1)
            {
                // First event in batch — start the window timer.
                _windowCts = new CancellationTokenSource(_batchWindow);
                _dispatchTask = DispatchOnWindowExpiry(_windowCts.Token, dispatchCallback);
            }
        }

        if (shouldDispatch)
        {
            await FlushAsync(dispatchCallback);
        }
    }

    /// <summary>
    /// Immediately dispatches all buffered events as a batch.
    /// </summary>
    public async Task FlushAsync(Func<BatchTriggerEvent, Task> dispatchCallback)
    {
        List<TriggerEvent> batch;
        lock (_lock)
        {
            batch = [.. _buffer];
            _buffer.Clear();
            _windowCts?.Cancel();
            _windowCts = null;
        }

        if (batch.Count > 0)
        {
            await dispatchCallback(new BatchTriggerEvent(batch));
        }
    }
}
```

#### 1.4 Batch dispatch: converting N events to 1

When the batch collector flushes, it needs to produce a single `TriggerEvent` from the collected events. Two approaches:

**Option A: Wrap in existing `BatchTriggerOutput<T>`**

If all events in the batch have the same trigger alias and output type, extract the individual outputs and wrap them:

```csharp
// Collected events: [TriggerEvent<ContentPublishedTriggerOutput>, ...]
// Dispatched as: TriggerEvent<BatchTriggerOutput<ContentPublishedTriggerOutput>>
//   with Items = [output1, output2, ...] and Count = N
```

This reuses the existing `ContentBatchPublishedTrigger` output model. The dispatched event uses the batch trigger alias (e.g., `umbracoAutomate.contentBatchPublished`) rather than the per-item alias.

**Option B: Generic batch wrapper**

Create a new generic batch event that works for any trigger type:

```csharp
public sealed class BatchedTriggerEvent : TriggerEvent
{
    public required IReadOnlyList<TriggerEvent> Events { get; init; }
    public int Count => Events.Count;
}
```

**Recommend Option A** for Phase 1 — it's compatible with the existing batch trigger infrastructure. Option B is more general but requires new handling throughout the dispatch pipeline.

#### 1.5 Trigger alias mapping

Each per-item trigger needs a corresponding batch alias mapping so the throttle knows which batch trigger to dispatch to:

```csharp
/// <summary>
/// Maps a per-item trigger alias to its batch counterpart.
/// Used by the throttle to dispatch collected events as batch trigger events.
/// </summary>
public interface IBatchTriggerAliasMap
{
    string? GetBatchAlias(string itemTriggerAlias);
}
```

Built-in mappings:
- `umbracoAutomate.contentPublished` → `umbracoAutomate.contentBatchPublished`

Add-on providers register their own mappings. If no mapping exists, batching falls back to the configured fallback strategy (rate limit or none).

#### 1.6 Idempotency for batch events

The batched event needs its own idempotency key. Reuse `IdempotencyKeyFactory.ForContentBatch()` — hash the sorted set of entity keys from the collected events. This ensures:
- The same batch of events always produces the same key (dedup)
- Different batches produce different keys (no false dedup)
- Batches that share some but not all events are distinct

### Phase 2: Automation-level batch configuration

#### 2.1 Trigger mode selection

Let users choose how their automation handles bulk events. Add to the automation trigger configuration:

```csharp
public enum TriggerMode
{
    /// <summary>
    /// One run per event (default, current behavior).
    /// </summary>
    PerItem = 0,

    /// <summary>
    /// Collect events within a window and start one run with a batch input.
    /// Requires the trigger to have a batch counterpart.
    /// </summary>
    Batched = 1,
}
```

```csharp
// New fields on TriggerConfiguration
public TriggerMode Mode { get; set; } = TriggerMode.PerItem;
public TimeSpan? BatchWindow { get; set; }
public int? MaxBatchSize { get; set; }
```

When `Mode == Batched`:
- The automation subscribes to the **batch** trigger alias instead of the per-item alias
- The trigger throttle routes per-item events into batch collection for this automation
- The `TriggerOutput` in the run contains an `Items` collection

When `Mode == PerItem` (default):
- Current behavior, no change

#### 2.2 Subscription-aware batching

The `TriggerThrottle` needs to know which automations want batched vs per-item delivery. The flow becomes:

```
ContentPublishedNotification (1 entity)
  → ContentPublishedTrigger.MapEvent() yields 1 per-item event
  → TriggerThrottle.SubmitAsync()
    → Check: any automation subscribes in Batched mode?
      → Yes: add to batch collector for that trigger alias
      → No per-item subscribers? Skip direct dispatch
    → Check: any automation subscribes in PerItem mode?
      → Yes: dispatch directly to outbox
```

A single notification can produce both a direct dispatch (for PerItem automations) and a batch-collected entry (for Batched automations). The throttle handles the fan-out.

### Phase 3: ForEach integration

With ForEach control flow implemented (`workflowcore-feature-gaps.md` #2):

1. Automation uses `Batched` trigger mode
2. `TriggerOutput["items"]` contains the batch array
3. First step is a ForEach that iterates `{{trigger.items}}`
4. Each iteration runs the configured steps with the individual item's data

```
Trigger (Batched: ContentPublished, window: 5s)
  → ForEach item in {{trigger.items}}
    → Log: "Processing {{item.contentName}}"
    → HTTP Request: POST to external API with {{item.contentKey}}
```

This depends on:
- ForEach control flow implementation
- Iteration context in binding resolution (`{{item.X}}` syntax)
- Per-iteration step run tracking

**Note on iteration parallelism:** WorkflowCore's `ForEach` is **parallel by default** — all iterations execute concurrently unless explicitly serialized. For batch processing this can be desirable (100 items processed in parallel rather than sequentially) but also risky (100 simultaneous HTTP requests may overwhelm a downstream service). The ForEach implementation should surface this as a user-configurable option ("parallel" vs "sequential"), and batch-mode automations should default to sequential processing to avoid surprising users with concurrent execution semantics.

### Relationship to Parallel execution (`workflowcore-feature-gaps.md` #1)

Batch + ForEach (parallel) overlaps with explicit Parallel execution. The difference:
- **Parallel** = fan out to N *different* branches, join at the end. Used when the branches do different things concurrently.
- **ForEach (parallel mode)** = fan out to N *identical* iterations of the same branch over different data. Used when the same logic runs per item.

Batch processing is the ForEach case, not the Parallel case. No direct dependency on Parallel, but users familiar with Zapier's "Paths" may expect a Parallel-style UI for batch processing — we should surface them as distinct features.

### Phase 4: Smart batching (future)

Intelligent batching strategies beyond time windows:

- **Content-type grouping** — batch only events of the same content type together (common for bulk operations on homogeneous content)
- **Transaction-aware batching** — detect Umbraco scope boundaries and batch all events within a single transaction
- **Adaptive windows** — start with a short window, extend if events keep arriving at high rate

These are optimization opportunities, not blockers for the core feature.

---

## UI considerations

### Trigger mode selector

In the automation trigger settings panel, add a "Processing mode" toggle:
- **Per item** (default) — "Run this automation once for each event"
- **Batched** — "Collect events and run once with all items"
  - Shows batch window and max batch size inputs when selected
  - Shows a note: "Use a ForEach step to process each item individually"

### Run viewer

For batch runs, the trigger data section should show:
- Batch size (number of items)
- Expandable list of items in the batch
- Time window during which events were collected

---

## Migration

No database changes for Phase 1 (in-memory batching).

Phase 2 adds `Mode`, `BatchWindow`, `MaxBatchSize` to the trigger configuration JSON stored in the `Automation` entity — no schema migration needed since trigger config is a JSON column.

---

## Testing

### Unit tests

- `BatchCollectorTests`:
  - Single event dispatches after window expires
  - Multiple events within window are collected into one batch
  - Batch dispatches immediately when max size reached
  - Flush on shutdown dispatches pending events
  - Empty buffer flush is a no-op
  - Events from different trigger aliases go to different collectors

- `TriggerThrottleBatchTests`:
  - Batch strategy routes events through batch collector
  - Batch dispatch produces correct batch trigger alias
  - Idempotency key is computed from batch contents
  - No batch alias mapping falls back to default strategy
  - Mixed PerItem and Batched subscribers both receive events

- `BatchTriggerAliasMapTests`:
  - Built-in mappings resolve correctly
  - Unknown alias returns null

### Integration tests

- Publish 10 content items in rapid succession with batch window of 2s
  - Verify 1 batch run starts (not 10)
  - Verify batch trigger output contains all 10 items
- Publish 150 items with max batch size of 100
  - Verify 2 batch runs start (100 + 50)
- Mix of PerItem and Batched automations on the same trigger
  - Verify PerItem automation gets 10 runs, Batched automation gets 1 run

---

## Open questions

1. **Should batch events preserve ordering?** The `BatchTriggerOutput.Items` list should maintain the order events were received. This matters for sequential processing in ForEach (process items in the order they were published).
2. **What if a batch contains duplicate entities?** If the same content is published twice within the batch window (e.g., rapid edit-publish cycles), both events appear in the batch. The automation can handle dedup within its ForEach logic, or we can deduplicate at the batch collector level using the entity key. Recommend: deduplicate by default (keep the latest event per entity key), with an option to preserve all.
3. **Should the batch window be reset on each new event (sliding) or fixed from the first event?** Fixed from first event is simpler and more predictable — the user knows the maximum delay before their batch runs. Sliding windows can delay indefinitely under sustained load. Recommend: fixed window, with the max-batch-size safety valve for high-volume scenarios.
4. **How does this interact with the trigger throttle from `trigger-flood-protection.md`?** Batching is a `ThrottleStrategy` option alongside RateLimit and Debounce. They're mutually exclusive strategies on the same trigger — you either rate-limit events, debounce them, or batch them. The `TriggerThrottle` service handles all three.
5. **Should batch runs count as 1 run or N runs for rate limiting purposes?** 1 run — the whole point of batching is to reduce run count. The per-automation rate limiter (`RateLimitService`) sees one run. Metrics should track both "batch runs" and "total items processed" for accurate reporting.
