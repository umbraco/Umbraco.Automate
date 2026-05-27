# Circuit Breaker: Auto-Disable on Error Threshold

> **Revised 2026-05-27** after validation against the codebase. Two mechanisms in the
> original draft were corrected:
> 1. **Integration point** — WorkflowCore `PostWorkflow` middleware → a run-completed
>    notification handler (middleware only fires on `Complete`, never on failure).
> 2. **Disable mechanism** — `IsEnabled = false` / unpublish → a dedicated
>    `AutomationHealth` state enforced at the single run chokepoint, leaving the publish
>    lifecycle untouched.
>
> See the [revision changelog](#revision-changelog) at the foot of this doc.

## Context

When an automation is misconfigured (wrong credentials, broken endpoint, invalid settings), it fails on every trigger. Today it will keep firing, failing, retrying, and burning resources indefinitely. There is no mechanism to detect a "sick" automation and stop it automatically.

Zapier handles this with a two-phase approach: warn the owner when the error rate climbs, then disable the automation if the threshold is breached. Lars Skjold Iversen (heavy Zapier user) confirmed this is always caused by misconfiguration — the automation won't succeed without human intervention, so disabling it loses nothing and protects the system.

### What we have today

- Per-step error handling: `StepErrorBehavior` (Retry, Suspend, Terminate, Compensate)
- Error classification: `IStepErrorClassifier` distinguishes terminal vs transient errors
- **Single run chokepoint**: `IAutomationExecutor.ExecuteAsync` is the only place a run record is created and a WorkflowCore workflow started — so every run passes through it regardless of caller. It has **three direct callers**: `TriggerEventHandler.cs:265` (all automatic triggers — event/scheduled/webhook, via the outbox; **ignores** the return value), `TriggerAutomationController.cs:77` (manual "run now", `InitiatorType = User`), and `ReplayRunController.cs:91` (replay). The latter two bypass `TriggerEventHandler` and call `ExecuteAsync` directly. It already gates on `IRateLimitService.CheckRateLimitAsync` before creating the run, which **throws** `RateLimitExceededException` on rejection (`AutomationExecutor.cs:50-59`, `RateLimitService.cs:43`). It receives `initiatorType` / `initiatorId`. Defined `TriggerInitiatorType` constants are `System` / `User` / `Webhook` / `Scheduled`; **replay passes a bare `"replay"` string** (`ReplayRunController.cs:93`) that is not currently a defined constant.
- **Terminal-state hook**: `RunFinalizer` publishes `AutomationRunCompletedNotification` for every terminal outcome (Complete, Terminated → Failed, failed-step → Failed) and for Suspended, after the run is persisted (`RunFinalizer.cs:75`). `RunCompletedNotificationDispatcher` already consumes it.
- Run history: `IAutomationRunService.GetRunsByAutomationPagedAsync()`, `GetRunSummaryAsync()`, `GetPreviousTerminalRunStatusAsync()`
- Automation lifecycle: `AutomationStatus` is **Draft / Published / Inactive**. Triggers fire only for `Published` (`TriggerEventHandler.cs:92`, `TriggerSubscriptionRegistry.cs:82`). `PublishAutomationAsync` / `UnpublishAutomationAsync` raise `AutomationPublishedNotification` / `AutomationUnpublishedNotification`.
- `GovernanceOptions` holds audit/governance configuration, including `DefaultNotifyOn`.

> **Note:** the old per-automation `IsEnabled` flag was **removed** (migration `UmbracoAutomate_RemoveAutomationIsEnabled`). There is no longer a boolean enable/disable separate from publish state — the only surviving `IsEnabled` is per notification channel (`ChannelConfiguration`).

### What's missing

- No per-automation health/error-rate tracking
- No warning notification before disabling
- No automatic disabling when thresholds are breached
- No run-start gate that can block a "sick" automation without changing its publish state
- No notification path for automation-lifecycle (vs run-status) events

---

## Design

The breaker introduces a **second axis** kept deliberately separate from the publish lifecycle:

| Axis | Type | Owned by | Values |
| --- | --- | --- | --- |
| **Publish intent** | `AutomationStatus` | the user | Draft / Published / Inactive |
| **System health** | `AutomationHealth` | the circuit breaker | Healthy / Degraded / Disabled |

An automation runs only when **both** allow it: `Status == Published` **AND** the circuit is closed. The breaker never touches `Status`. This keeps user intent distinct from system health, so:

- the UI can show "auto-disabled — repeated failures" on an automation that is *still* `Published`;
- version diffs stay clean (`CompareVersions` diffs `Status`, not health);
- "re-enable" is unambiguous and doesn't run the heavyweight unpublish/publish pipeline.

### Phase 1: Health tracking + auto-disable (backend only)

#### 1.1 New options: `CircuitBreakerOptions`

Add bound to `Umbraco:Automate:CircuitBreaker` (and register in `appsettings-schema.Umbraco.Automate.json`):

```csharp
public sealed class CircuitBreakerOptions
{
    /// <summary>Whether the circuit breaker is enabled globally.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Number of consecutive failures before disabling an automation.</summary>
    public int ConsecutiveFailureThreshold { get; set; } = 10;

    /// <summary>Error rate (0.0–1.0) over the evaluation window that triggers a warning.</summary>
    public double WarningErrorRate { get; set; } = 0.5;

    /// <summary>Error rate (0.0–1.0) over the evaluation window that triggers auto-disable.</summary>
    public double DisableErrorRate { get; set; } = 0.7;

    /// <summary>
    /// Number of recent terminal runs to evaluate for error rate calculation.
    /// Error-rate thresholds only apply once at least this many terminal runs exist.
    /// </summary>
    public int EvaluationWindowSize { get; set; } = 20;

    /// <summary>
    /// Grace period after a warning before auto-disable can trigger, giving the owner
    /// time to investigate and fix.
    /// </summary>
    public TimeSpan GracePeriodAfterWarning { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Whether a manual "run now" / replay is allowed while the circuit is open, so the
    /// owner can test a fix. The run is a pure test — success does NOT auto re-enable;
    /// the owner must re-enable explicitly (see 1.6).
    /// </summary>
    public bool AllowManualRunWhileDisabled { get; set; } = true;
}
```

> **Defaults vs trigger cadence:** `ConsecutiveFailureThreshold = 10` trips in minutes for a webhook but ~10 days for a daily `ScheduledTrigger`. That's acceptable — the error-rate + grace path is the real safety net for low-frequency triggers — but worth keeping in mind when tuning.

#### 1.2 New domain model: `AutomationHealthState` (dedicated table, **not** fields on `Automation`)

Store health in its own table keyed by automation id. **Do not** add these as fields on the `Automation` entity: `AutomationVersionableEntityAdapter.CreateSnapshot` JSON-serialises the *whole* entity into every version snapshot, so runtime fields would be frozen into snapshots and **restored stale on rollback**. A separate table keeps runtime health off the versioned definition entirely.

```csharp
public sealed class AutomationHealthState
{
    public Guid AutomationId { get; set; }                          // PK / FK to Automation
    public AutomationHealth Health { get; set; } = AutomationHealth.Healthy;
    public DateTime? WarningIssuedUtc { get; set; }                 // grace-period clock
    public DateTime? DisabledUtc { get; set; }
}

public enum AutomationHealth
{
    Healthy = 0,
    Degraded = 1,   // Warning issued, within grace period
    Disabled = 2,   // Auto-disabled by the circuit breaker
}
```

> **No persisted `ConsecutiveFailures` counter.** Derive both the consecutive count and the error rate from run history in a single ordered query (see [1.7](#17-run-query-method)). A denormalised counter mutated on every run is both redundant and a concurrency hot-spot. The durable state is just `Health` plus the two timestamps (`WarningIssuedUtc`, `DisabledUtc`) — no per-run failure counter.

#### 1.3 New service: `ICircuitBreakerService`

```csharp
public interface ICircuitBreakerService
{
    /// <summary>
    /// Evaluates health after a run reaches a terminal state. May issue a warning,
    /// or disable the automation. Idempotent.
    /// </summary>
    Task EvaluateAsync(AutomationRun run, CancellationToken ct);

    /// <summary>
    /// Run-start gate. Returns false when the circuit is open for this initiator
    /// (see <see cref="CircuitBreakerOptions.AllowManualRunWhileDisabled"/>).
    /// </summary>
    Task<bool> IsRunAllowedAsync(Guid automationId, string initiatorType, CancellationToken ct);

    /// <summary>
    /// Clears health back to Healthy. Called by the explicit Re-enable action and on re-publish.
    /// Never called automatically off the back of a run.
    /// </summary>
    Task ResetAsync(Guid automationId, CancellationToken ct);
}
```

**`EvaluateAsync` logic:**

1. If `Options.Enabled == false` → no-op.
2. If the run **succeeded**:
   - If `Health == Degraded`, reset to `Healthy` and clear `WarningIssuedUtc` (a still-running automation recovered — this clears the warning; it resumes nothing that was stopped).
   - If `Health == Disabled`, **do not auto-reset.** Only human-initiated test runs reach here while disabled; leave the circuit open and require an explicit Re-enable. (The UI can still derive a "re-enable?" nudge from this state — the latest run succeeding while `Disabled` — with no new persisted field; see [§2.2](#22-frontend).)
   - Return.
3. If the run was **Suspended** → no-op. Suspend is an *intentional* wait-for-intervention (error-mode Suspend / approval waits), not a failure.
4. If the run **failed** → load the recent window ([1.7](#17-run-query-method)), compute consecutive-failure count + error rate, and apply these checks **in order (first match wins, short-circuits)**:
   - **Consecutive threshold**: `consecutive >= ConsecutiveFailureThreshold` → **disable**.
   - **Disable rate**: `rate >= DisableErrorRate` AND `WarningIssuedUtc` set AND grace elapsed → **disable**.
   - **Warning rate**: `rate >= WarningErrorRate` AND `Health == Healthy` → **warn** (`Health = Degraded`, set `WarningIssuedUtc`, emit health-changed notification).
5. **Disable** means:
   - Set `Health = Disabled`, `DisabledUtc = UtcNow`. **`Status` is untouched** (stays `Published`).
   - Emit an `AutomationHealthChangedNotification` (see [1.8](#18-disablewarning-notification-dedicated-notification)).
   - Log the event.
   - **Idempotent**: if already `Disabled`, no-op — concurrent runs (`MaxConcurrentRuns` defaults to 10) can evaluate simultaneously, so guard against double-disable.

#### 1.4 Enforcement: gate in `IAutomationExecutor.ExecuteAsync`

Add the circuit check inside `ExecuteAsync`, immediately beside the existing rate-limit gate:

```csharp
// AutomationExecutor.ExecuteAsync — before the run record is created
await _rateLimitService.CheckRateLimitAsync(automation.Id, cancellationToken);

if (!await _circuitBreaker.IsRunAllowedAsync(automation.Id, initiatorType, cancellationToken))
{
    _logger.LogDebug(
        "Run skipped — circuit open for automation {AutomationId} (initiator {Initiator})",
        automation.Id, initiatorType);
    return Guid.Empty; // quiet skip — no run record, no workflow start (interactive callers gate earlier; see note below)
}
```

Because `ExecuteAsync` is the single point at which any run record is created and any workflow started, gating here covers **all** entry points. This is not merely convenient — it's necessary: manual runs (`TriggerAutomationController.cs:77`) and replays (`ReplayRunController.cs:91`) call `ExecuteAsync` **directly**, bypassing `TriggerEventHandler`, so a gate in the dispatcher alone would miss them.

- **Quiet skip on the automatic path; explicit signal on the manual paths.** The rate-limit gate *throws* `RateLimitExceededException` (`RateLimitService.cs:43`); the breaker should **not** throw on the blocked path. A tripped automation may receive many trigger events, and `TriggerEventHandler.cs:265` ignores the return value, so a quiet debug-logged skip avoids per-event noise and message-bus retries. The two Web controllers, however, *do* consume the return value to answer the UI — so a bare `Guid.Empty` sentinel is insufficient for them. They should call `IsRunAllowedAsync` first (or `ExecuteAsync` should signal "blocked" distinctly) and return `409 Conflict` ("automation is auto-disabled") when a run is refused.
- **Per-initiator policy.** "Allowed while disabled" should match **human-initiated** runs — `initiatorType` of `User` (manual "run now") **or** `replay`. Do **not** hard-code `== User`: replay passes the bare string `"replay"` (`ReplayRunController.cs:93`), so a `User`-only check would wrongly block it. This feature adds an `IsInteractive(initiatorType)` predicate matching `TriggerInitiatorType.User` and the `"replay"` literal — that is all the breaker needs, and it works whether or not the literal is later promoted to a constant (a separate cleanup; see [Out of scope](#out-of-scope)). The predicate localises the magic string so that swap is a one-liner. `System` / `Scheduled` / `Webhook` are blocked. Gated by `AllowManualRunWhileDisabled`. The run is a **pure test**: success does **not** close the circuit — re-instatement is always explicit (see [1.6](#16-reset)).

> The existing `Status == Published` filters (`TriggerEventHandler.cs:92`, `TriggerSubscriptionRegistry.cs:82`) remain **coarse pre-filters / optimisations** — the `ExecuteAsync` gate is now the authoritative "will it run" decision, so do not duplicate the health check into them. In particular, leave disabled-but-published automations in `TriggerSubscriptionRegistry`'s alias set: removing them would couple that cache to health changes (invalidation on every trip/reset) for no correctness gain, since events still reach `ExecuteAsync` and are skipped there.

#### 1.5 Evaluation trigger: run-completed notification handler (**not** middleware)

Implement evaluation as a second `INotificationAsyncHandler<AutomationRunCompletedNotification>`, alongside `RunCompletedNotificationDispatcher`:

```csharp
internal sealed class CircuitBreakerEvaluator
    : INotificationAsyncHandler<AutomationRunCompletedNotification>
{
    private readonly ICircuitBreakerService _circuitBreaker;

    public Task HandleAsync(AutomationRunCompletedNotification notification, CancellationToken ct)
        => _circuitBreaker.EvaluateAsync(notification.Run, ct);
}
```

**Why a notification handler and not `IWorkflowMiddleware` (as the original draft proposed):**

- WorkflowCore's `PostWorkflow` middleware runs **only when the workflow reaches `Complete`** — verified against the 3.9.0 source: the post-middleware runner is gated behind `workflow.Status = WorkflowStatus.Complete` in `WorkflowExecutor`. It does **not** run on `Terminated`, errored, or `Suspended`. A circuit breaker exists precisely to catch automations that fail/terminate on every trigger — those never reach the middleware, so the breaker would **never trip**. This is a correctness defect, not a style preference.
- Workflow-level middleware (`workflowcore-feature-gaps.md` #11) isn't implemented yet, so the original plan also depended on unbuilt infrastructure.
- `RunFinalizer` already publishes `AutomationRunCompletedNotification` for **all** terminal outcomes including Failed/Terminated (`RunFinalizer.cs:75`), fires after the run is persisted (so the handler can safely query run history including the just-finished run), and works in both `SchedulerOnly` and `Distributed` execution modes. It is the complete, proven hook.

> The original "ordering: breaker after notification dispatch" concern is moot — the breaker emits its *own* health-changed notification ([1.8](#18-disablewarning-notification-dedicated-notification)); the failed-run notification describes the run, not the automation's enabled state, so there's no pre/post-disable race.

**Isolate failures.** `CircuitBreakerEvaluator` shares `AutomationRunCompletedNotification` with `RunCompletedNotificationDispatcher`, so wrap `EvaluateAsync` in a try/catch and log on failure — a breaker fault must never disrupt user-facing run notifications (or any other handler on that notification). Combined with the idempotency guarantee in [1.3](#13-new-service-icircuitbreakerservice), this keeps evaluation safe to retry and incapable of taking down the dispatch pipeline.

#### 1.6 Reset

Re-instatement is always an **explicit** operator action; a successful manual/replay run never silently resumes automatic execution.

- **Re-enable button (canonical):** surfaced in the automation **list view** and the automation **workspace footer (bottom action bar)** whenever `Health == Disabled` → calls `ResetAsync` (Health → Healthy, clear timestamps).
- **Re-publish:** subscribe to the existing `AutomationPublishedNotification` (raised by `PublishAutomationAsync`, `AutomationService.cs:180`) and call `ResetAsync`. Editing + republishing a fixed definition resets the circuit. No edit to `IAutomationService` required — consistent with the notification-handler pattern in 1.5.
- **Manual/replay runs are pure tests.** A successful test while disabled does **not** auto-close the circuit (avoids implicit resumption and a one-lucky-success flap). The UI can still surface a "re-enable?" nudge by detecting that the latest run succeeded while `Health == Disabled` — no new persisted state required (see [§2.2](#22-frontend)).

> **Why explicit fits the platform:** the manual run is fire-and-forget — `postAutomationsByIdTrigger` returns once the run has *started*, not completed (`automation-run-now.action.ts` shows a "run started" toast; the run finishes asynchronously). The UI can't reliably show "succeeded" inline regardless — the owner observes completion in the run views, then clicks Re-enable. The `409`-on-blocked handling this relies on already exists in that same action.

#### 1.7 Run query method

Add to `IAutomationRunService`:

```csharp
/// <summary>
/// Returns the most recent N terminal run statuses (newest first) for an automation —
/// enough to compute both consecutive-failure count and error rate in a single query.
/// </summary>
Task<IReadOnlyList<AutomationRunStatus>> GetRecentTerminalStatusesAsync(
    Guid automationId, int windowSize, CancellationToken ct);
```

A single `SELECT TOP(N) Status ... WHERE terminal ORDER BY CompletedUtc DESC` — no full run entities loaded. Consecutive failures = count of leading `Failed`; error rate = `Failed / Total` over the window. Mirrors the lightweight shape of the existing `GetPreviousTerminalRunStatusAsync`.

#### 1.8 Disable/warning notification: dedicated notification

The new notification events do **not** fit the existing dispatcher: `RunCompletedNotificationDispatcher.ShouldNotifyAsync` switches on `run.Status` (`RunCompletedNotificationDispatcher.cs:118`), but "disabled" / "degraded" are *automation-lifecycle* events, not run statuses. So:

- The breaker emits a dedicated `AutomationHealthChangedNotification` (carrying automation id, old/new health, reason).
- A small handler dispatches it to the automation's configured channels, **reusing** the channel resolution and message-building currently inside `RunCompletedNotificationDispatcher` (extract the shared pieces rather than duplicating them).
- Add `NotifyOn` flags as channel opt-in filters consumed by that handler — they don't collide (current max is `Recovered = 8`):

```csharp
/// <summary>Notify when the automation is auto-disabled by the circuit breaker.</summary>
Disabled = 16,

/// <summary>Notify when the circuit breaker issues a degradation warning.</summary>
Warning = 32,
```

`GovernanceOptions.DefaultNotifyOn` should include `Disabled` (operators always want to know when an automation is auto-disabled).

### Phase 2: API + UI

#### 2.1 Management API

- `GET /automation/{id}/health` — returns health state and warning/disabled timestamps (from the health table).
- Health state included in the existing automation detail response.
- New filter on the automation list: `?health=degraded` / `?health=disabled`.

#### 2.2 Frontend

- Health badge on automation list and detail views (green / amber / red) — shown independently of publish status.
- **"Re-enable" button** on `Disabled` automations, surfaced in the **list view** and the **automation workspace footer (bottom action bar)** → calls `ResetAsync`. Together with re-publish this is the *only* way auto-disable is cleared; no run auto-re-enables.
- **Banner on automation detail:** amber for `Degraded` (warning), red for `Disabled` (auto-disabled, carrying the Re-enable button). When the latest run `Completed` while still `Disabled`, the red banner adds a "test succeeded — re-enable?" nudge — derived from run status, so no extra state and no toast-with-button.
- Manual "run now" / replay stay available while disabled (if `AllowManualRunWhileDisabled`) as fire-and-forget tests; the existing run views show whether the test succeeded.
- Per-workspace threshold overrides in workspace admin (future — see open question 2).

> No new notification infrastructure is needed: `peek` toasts, the `409` problem-detail handling in `automation-run-now.action.ts`, run-status views, and the realtime listener (`core/realtime/editor-notification.listener.ts`) already exist. The only new surfaces are the health badge, the detail banner, and the Re-enable button (list + footer).

---

## Migration

- New table `umbracoAutomateAutomationHealth`:
  - `AutomationId` (PK, FK → `umbracoAutomateAutomation`)
  - `Health` (int, default 0)
  - `WarningIssuedUtc` (datetime, nullable)
  - `DisabledUtc` (datetime, nullable)
- Migration prefix: `UmbracoAutomate_`
- No data migration needed — an automation with no health row is implicitly `Healthy`.

---

## Testing

### Unit tests

- `CircuitBreakerServiceTests`:
  - Successful run clears the `Degraded` (warning) state back to `Healthy`
  - Suspended run is ignored (not counted as failure)
  - Consecutive failures reaching threshold triggers disable
  - Error rate exceeding warning threshold issues warning (sets `WarningIssuedUtc`)
  - Error rate exceeding disable threshold after grace period triggers disable
  - Error rate exceeding disable threshold **within** grace period does NOT disable
  - Re-publish (`AutomationPublishedNotification`) resets health state
  - Disable is idempotent under concurrent evaluation (no double-disable)
  - Circuit breaker disabled via options does nothing
- `ExecuteAsync` gate:
  - `IsRunAllowedAsync` returns false when `Disabled` and initiator is `System` / `Scheduled` / `Webhook` → no run record created
  - Human-initiated runs (`User` manual run **and** `replay`) allowed when `AllowManualRunWhileDisabled` → run proceeds; **success does NOT auto-close** (stays `Disabled` until explicit re-enable)
  - Human-initiated runs blocked when `AllowManualRunWhileDisabled == false`
  - Web controllers (`TriggerAutomationController`, `ReplayRunController`) return `409 Conflict` when a run is refused
  - Explicit `ResetAsync` (Re-enable) clears `Disabled` → `Healthy`; re-publish (`AutomationPublishedNotification`) also clears it

### Integration tests

- Full flow: create automation, simulate N failed runs via the run-completed notification, verify health transitions, the `ExecuteAsync` gate, and the health-changed notification fire as expected.

---

## Out of scope

- **`TriggerInitiatorType.Replay` constant** — tracked in [#63](https://github.com/umbraco/Umbraco.Automate/issues/63). Replay currently passes a bare `"replay"` string (`ReplayRunController.cs:93`) that is not a defined `TriggerInitiatorType`. Promoting it to a constant — and switching `ReplayRunController` and the `IsInteractive` predicate over to it — is agreed, but **tracked as separate work**. This feature depends only on the `IsInteractive` predicate (which matches the literal), so it neither needs nor blocks the constant. Keeps the breaker change focused; the constant can land on its own schedule.

---

## Open questions

1. ~~Should auto-disable set `IsEnabled = false` or call `UnpublishAutomationAsync`?~~ **Resolved:** neither. `IsEnabled` no longer exists, and unpublishing conflates system health with user intent. Use a dedicated `AutomationHealth` state enforced at the `ExecuteAsync` gate, leaving `Status` untouched.
2. **Per-workspace threshold overrides?** Useful for advanced governance but adds complexity. Defer to Phase 2.
3. **Should a successful run clear the *degraded* (warning) state?** Yes — while Degraded the automation still runs, so a success clears the warning back to Healthy. Distinct from *disabled*, which never auto-clears (see #4).
4. ~~Should a manual run be allowed while disabled, and should success close the circuit?~~ **Resolved:** manual/replay runs are allowed while disabled (`AllowManualRunWhileDisabled = true`) as **pure tests**; success does **not** auto-close. Re-instatement is **explicit only** — a Re-enable button in the list view / workspace footer, or re-publish. Practical reinforcement: manual runs are fire-and-forget, so the UI can't show inline success regardless.

---

## Revision changelog

| Area | Original draft | Revised |
| --- | --- | --- |
| Evaluation trigger | `IWorkflowMiddleware` (`PostWorkflow`) | `INotificationAsyncHandler<AutomationRunCompletedNotification>` — middleware only fires on `Complete`, never on failure/terminate |
| Disable mechanism | `IsEnabled = false` (field removed) / unpublish | Dedicated `AutomationHealth` state + gate in `ExecuteAsync` (single run chokepoint); `Status` untouched |
| State storage | Fields on `Automation` | Separate `AutomationHealthState` table — avoids version-snapshot/rollback contamination |
| Failure counting | Persisted `ConsecutiveFailures` counter | Derived from run history (`GetRecentTerminalStatusesAsync`) |
| Disable/warning notify | `NotifyOn` flags via run-status dispatcher | Dedicated `AutomationHealthChangedNotification` (lifecycle event, not run status); `NotifyOn.Disabled/Warning` as channel opt-in filters |
| Reset on re-publish | Edit `PublishAutomationAsync` to call `ResetAsync` | Subscribe to existing `AutomationPublishedNotification` |
| Reinstatement | Auto-close on successful manual run | **Explicit only** — Re-enable button (list view + workspace footer) or re-publish; manual/replay runs are pure tests that never auto-close |
