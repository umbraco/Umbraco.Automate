# Circuit Breaker: Auto-Disable on Error Threshold

## Context

When an automation is misconfigured (wrong credentials, broken endpoint, invalid settings), it fails on every trigger. Today it will keep firing, failing, retrying, and burning resources indefinitely. There is no mechanism to detect a "sick" automation and stop it automatically.

Zapier handles this with a two-phase approach: warn the owner when the error rate climbs, then disable the automation if the threshold is breached. Lars Skjold Iversen (heavy Zapier user) confirmed this is always caused by misconfiguration — the automation won't succeed without human intervention, so disabling it loses nothing and protects the system.

### What we have today

- Per-step error handling: `StepErrorBehavior` (Retry, Suspend, Terminate, Compensate)
- Error classification: `IStepErrorClassifier` distinguishes terminal vs transient errors
- Run completion notifications: `RunCompletedNotificationDispatcher` sends via configured channels when `NotifyOn` flags match (Failed, Suspended, Completed, Recovered)
- Run history: `IAutomationRunService.GetRunsByAutomationPagedAsync()` and `GetRunSummaryAsync()`
- Automation lifecycle: `IAutomationService.UnpublishAutomationAsync()` exists and is functional
- `GovernanceOptions` already holds audit/governance configuration

### What's missing

- No tracking of error rate or consecutive failures per automation
- No warning notification before disabling
- No automatic disabling when thresholds are breached
- No `NotifyOn` flag for "degraded" or "warning" state

---

## Design

### Phase 1: Error tracking + auto-disable (backend only)

#### 1.1 New options: `CircuitBreakerOptions`

Add to `AutomateOptions.cs` (or as a new class bound to `Umbraco:Automate:CircuitBreaker`):

```csharp
public sealed class CircuitBreakerOptions
{
    /// <summary>
    /// Whether the circuit breaker is enabled globally.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of consecutive failures before disabling an automation.
    /// </summary>
    public int ConsecutiveFailureThreshold { get; set; } = 10;

    /// <summary>
    /// Error rate (0.0–1.0) over the evaluation window that triggers a warning.
    /// </summary>
    public double WarningErrorRate { get; set; } = 0.5;

    /// <summary>
    /// Error rate (0.0–1.0) over the evaluation window that triggers auto-disable.
    /// </summary>
    public double DisableErrorRate { get; set; } = 0.7;

    /// <summary>
    /// Number of recent terminal runs to evaluate for error rate calculation.
    /// Must have at least this many runs before error rate thresholds apply.
    /// </summary>
    public int EvaluationWindowSize { get; set; } = 20;

    /// <summary>
    /// Grace period after a warning before auto-disable can trigger.
    /// Gives the owner time to investigate and fix.
    /// </summary>
    public TimeSpan GracePeriodAfterWarning { get; set; } = TimeSpan.FromHours(24);
}
```

#### 1.2 New domain model: `AutomationHealthState`

Track health state per automation. This could be a new entity or fields on the existing `Automation` entity. Prefer fields on `Automation` to avoid a new table:

```csharp
// New fields on Automation entity
public AutomationHealth Health { get; set; } = AutomationHealth.Healthy;
public int ConsecutiveFailures { get; set; }
public DateTime? WarningIssuedUtc { get; set; }
public DateTime? DisabledUtc { get; set; }
```

```csharp
public enum AutomationHealth
{
    Healthy = 0,
    Degraded = 1,   // Warning issued, within grace period
    Disabled = 2,   // Auto-disabled by circuit breaker
}
```

#### 1.3 New service: `ICircuitBreakerService`

```csharp
public interface ICircuitBreakerService
{
    /// <summary>
    /// Evaluates the health of an automation after a run completes.
    /// May issue a warning notification, or disable the automation.
    /// </summary>
    Task EvaluateAsync(Guid automationId, AutomationRunStatus runStatus, CancellationToken ct);

    /// <summary>
    /// Resets the circuit breaker state for an automation.
    /// Called when an automation is re-published after being disabled.
    /// </summary>
    Task ResetAsync(Guid automationId, CancellationToken ct);
}
```

**Implementation logic for `EvaluateAsync`:**

1. If the run succeeded:
   - Reset `ConsecutiveFailures` to 0
   - If `Health == Degraded`, set back to `Healthy` and clear `WarningIssuedUtc`
   - Return early

2. If the run failed:
   - Increment `ConsecutiveFailures`
   - **Check consecutive threshold**: if `ConsecutiveFailures >= ConsecutiveFailureThreshold`, disable immediately
   - **Check error rate threshold**: query the last N terminal runs (`EvaluationWindowSize`) and calculate failure rate
     - If rate >= `DisableErrorRate` AND (`WarningIssuedUtc` is set AND grace period has elapsed), disable
     - If rate >= `WarningErrorRate` AND `Health == Healthy`, issue warning and set `Health = Degraded`

3. **Disable** means:
   - Set `Health = Disabled`, `DisabledUtc = DateTime.UtcNow`
   - Set `IsEnabled = false` on the automation (keeps it published but inactive — triggers won't fire)
   - Send a notification via the automation's configured channels with a new `NotifyOn.Disabled` flag
   - Log the event

#### 1.4 New `NotifyOn` flag

Add to the existing `NotifyOn` flags enum:

```csharp
/// <summary>Notify when the automation is auto-disabled by the circuit breaker.</summary>
Disabled = 16,

/// <summary>Notify when the circuit breaker issues a degradation warning.</summary>
Warning = 32,
```

Default `GovernanceOptions.DefaultNotifyOn` should include `Disabled` (always want to know when an automation is auto-disabled).

#### 1.5 Integration point: `IWorkflowMiddleware` (WorkflowCore)

Implement the circuit breaker as a WorkflowCore post-execution middleware. `IWorkflowMiddleware` runs pre/post the entire workflow and is explicitly designed for cross-cutting governance concerns like this.

```csharp
internal sealed class CircuitBreakerMiddleware : IWorkflowMiddleware
{
    public WorkflowMiddlewarePhase Phase => WorkflowMiddlewarePhase.PostWorkflow;

    public async Task HandleAsync(
        WorkflowInstance workflow,
        WorkflowDelegate next,
        CancellationToken cancellationToken)
    {
        await next();

        // After the workflow completes (success, failure, or terminate),
        // evaluate the circuit breaker for the automation.
        if (workflow.Data is AutomationWorkflowData data)
        {
            var runStatus = MapWorkflowStatus(workflow.Status);
            await _circuitBreakerService.EvaluateAsync(
                data.AutomationId, runStatus, cancellationToken);
        }
    }
}
```

**Why middleware over the notification dispatcher:**
- Runs inside the engine's execution context — cleaner separation of concerns
- Fires for all workflow terminations, not just those that reach the notification path
- Aligned with the planned workflow-level middleware feature (see `workflowcore-feature-gaps.md` #11)
- Lets the `RunCompletedNotificationDispatcher` stay focused on user-facing notifications

**Dependency:** requires workflow-level middleware infrastructure (`workflowcore-feature-gaps.md` #11, flagged as Phase 1 low-effort). If not yet implemented, the circuit breaker is a good forcing function to land it.

**Ordering:** the circuit breaker middleware should run **after** notification dispatch so notifications reflect the pre-disable state. Configure via registration order.

#### 1.6 Integration point: `IAutomationService.PublishAutomationAsync`

When an automation is re-published, call `ICircuitBreakerService.ResetAsync()` to clear the health state. The assumption is that the user has fixed the misconfiguration.

#### 1.7 Run query method

Add to `IAutomationRunService`:

```csharp
/// <summary>
/// Gets the error rate for an automation over the most recent N terminal runs.
/// Returns (failedCount, totalCount) for threshold evaluation.
/// </summary>
Task<(int Failed, int Total)> GetRecentErrorCountsAsync(
    Guid automationId, int windowSize, CancellationToken ct);
```

This avoids loading full run entities — a simple `COUNT` + `WHERE` query.

### Phase 2: API + UI

#### 2.1 Management API

- `GET /automation/{id}/health` — returns health state, consecutive failures, warning/disabled timestamps
- Health state included in existing automation detail response
- New filter on automation list: `?health=degraded` / `?health=disabled`

#### 2.2 Frontend

- Health badge on automation list and detail views (green/amber/red)
- "Re-enable" action on disabled automations (calls publish, which resets the circuit breaker)
- Warning banner on automation detail when `Health == Degraded`
- Governance settings in workspace admin for overriding thresholds per workspace (future)

---

## Migration

- New columns on `Automation` table: `Health` (int, default 0), `ConsecutiveFailures` (int, default 0), `WarningIssuedUtc` (datetime, nullable), `DisabledUtc` (datetime, nullable)
- Migration prefix: `UmbracoAutomate_`
- No data migration needed — all existing automations start as `Healthy`

---

## Testing

### Unit tests

- `CircuitBreakerServiceTests`:
  - Successful run resets consecutive failures and clears degraded state
  - Failed run increments consecutive failures
  - Consecutive failures reaching threshold triggers disable
  - Error rate exceeding warning threshold issues warning
  - Error rate exceeding disable threshold after grace period triggers disable
  - Error rate exceeding disable threshold within grace period does NOT trigger disable
  - Re-publish resets health state
  - Disabled automation stops evaluating (idempotent)
  - Circuit breaker disabled via options does nothing

### Integration tests

- Full flow: create automation, simulate N failed runs, verify health transitions and notifications

---

## Open questions

1. **Should auto-disable set `IsEnabled = false` or call `UnpublishAutomationAsync`?** Setting `IsEnabled = false` is lighter — the draft is preserved and re-publishing is a one-click fix. Unpublishing is heavier and may lose the "published" state. Recommend `IsEnabled = false`.
2. **Per-workspace threshold overrides?** Useful for advanced governance but adds complexity. Defer to Phase 2.
3. **Should the grace period reset if a successful run occurs during the degraded state?** Yes — a successful run proves the fix may be working, so reset to Healthy.
