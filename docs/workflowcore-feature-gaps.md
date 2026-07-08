# WorkflowCore Feature Gaps

Outstanding WorkflowCore capabilities not yet surfaced in Umbraco.Automate. Items are grouped by theme and ordered by priority within each group.

> **Note:** "surfaced" does not always mean "used natively". The control-flow items below
> (ForEach, While, Parallel, and the If/Switch conditionals) shipped as **custom container
> step bodies** rather than via WorkflowCore's fluent builder, for reasons recorded in
> [control-flow-architecture.md](control-flow-architecture.md). Entries marked
> **✅ Implemented** are done; the rest are still open.

---

## Control Flow

### 1. Parallel Execution ✅ Implemented

> **Implemented** as `ParallelContainerStepBody` (custom — see [control-flow-architecture.md](control-flow-architecture.md)). Original gap analysis retained below for context.

**WorkflowCore:** `Parallel()` + `.Join()` — run multiple branches concurrently and wait for all to complete before continuing.

**Why it matters:** Users can't run independent steps concurrently (e.g., send a Slack notification AND update an external CRM at the same time). Every automation is strictly sequential today, which wastes time when steps have no data dependency on each other.

**User-facing concept:** "Run in parallel" split/join in the automation editor.

**Complexity:** High — requires UI for branch management, step graph must support fan-out/fan-in, and run tracking needs per-branch status.

---

### 2. ForEach (Collection Iteration) ✅ Implemented

> **Implemented** as `ForEachContainerStepBody` (custom — see [control-flow-architecture.md](control-flow-architecture.md)). Performance of this body was subsequently hardened in PR #128 (collection materialised once per loop; iteration state pruned from the persisted workflow data) and again by normalising WorkflowCore execution pointers into their own table — one delta-written row per pointer instead of re-serialising the entire (unboundedly growing) instance as a single JSON blob every execution pass. That removed the O(n²) persistence cost that dominated large loops; see the persistence section of [engineering-spec.md](engineering-spec.md). Original gap analysis retained below for context.

**WorkflowCore:** `ForEach()` — execute a block of steps for each item in a collection (parallel by default).

**Why it matters:** Extremely common automation pattern. "For each content item matching this query, publish it." "For each form entry, create a member." Without this, users must build one-at-a-time automations or use external tools.

**User-facing concept:** "For each item" loop step that iterates over an output collection from a previous step.

**Complexity:** High — needs collection-aware binding resolution, iteration context injection, and UI to define the loop body.

---

### 3. While (Conditional Loop) ✅ Implemented

> **Implemented** as `WhileContainerStepBody` (custom — see [control-flow-architecture.md](control-flow-architecture.md)), including the max-iteration safety guard noted below. Original gap analysis retained below for context.

**WorkflowCore:** `While()` — repeat a block of steps as long as a condition is true.

**Why it matters:** Enables polling patterns ("keep checking until this condition is met") and retry-with-transform flows. Less common than ForEach but important for integration scenarios where an external system must be polled.

**User-facing concept:** "Repeat while" loop step with a condition expression.

**Complexity:** Medium — simpler than ForEach (no collection context), but needs loop-count safeguards to prevent infinite loops.

---

### 4. Schedule (Background Fork)

**WorkflowCore:** `Schedule()` — execute a block of steps in the background after a delay, without blocking the main flow.

**Why it matters:** Enables fire-and-forget side effects. "Continue the main automation immediately, but also send a follow-up email in 30 minutes." Currently, DelayAction blocks the entire automation.

**User-facing concept:** "Schedule for later" step that forks a background branch.

**Complexity:** Medium — needs UI to distinguish blocking delay from background schedule, and run tracking for the forked branch.

---

### 5. Recur (Interval Repetition)

**WorkflowCore:** `Recur()` — repeatedly execute a block of steps at a fixed interval until a stop condition is met.

**Why it matters:** Useful for monitoring patterns within a running automation. "Every 5 minutes, check if the approval has been granted externally." Distinct from ScheduledTrigger (which starts a new run) — this loops within a single run.

**User-facing concept:** "Repeat every [interval] until [condition]" step.

**Complexity:** Medium — similar to While but time-driven. Needs interval validation and stop-condition safeguards.

---

## Error Handling & Transactions

### 6. Saga Transactions with Compensation

**WorkflowCore:** `Saga()` + `CompensateWith()` — group steps into a transaction. If any step fails, compensation steps run in reverse order to undo prior work.

**Why it matters:** Multi-system automations are fragile without rollback. If step 3 creates a record in an external CRM but step 4 fails, there's no way to undo the CRM record today. Saga support makes automations production-safe for critical business processes.

**User-facing concept:** "Transaction" grouping in the editor, with per-step "undo action" configuration.

**Complexity:** High — needs compensation step authoring in UI, reverse-order execution logic, and clear run history showing compensation activity.

---

### 7. Native OnError Per-Step

**WorkflowCore:** `.OnError(WorkflowErrorHandling.Retry | Suspend | Terminate, retryInterval)` — built-in per-step error policy.

**Current state:** Reimplemented via custom `ErrorHandlingMiddleware` and `StepConfiguration.ErrorBehavior`. Works, but doesn't support `Suspend` (pause workflow for manual intervention on error).

**Gap:** The `Suspend` error mode — pause the workflow on failure and let a user resume after fixing the issue. Currently only Retry and Terminate are available.

**Complexity:** Low — mostly wiring the suspend state into the existing error handling middleware and exposing it in the step configuration UI.

---

## Execution Control

### 8. CancelCondition ⚠️ Partially addressed

> **External run cancellation** (the "cancelled by a user" motivation below) is now handled by
> `RunCancellationStepMiddleware` (PR #127) — cooperatively, before every step. That path
> deliberately does **not** use native `.CancelCondition`, because the cancel signal lives in
> the durable run row (set by the API / another node) and native `CancelCondition` only sees the
> in-memory `workflow.Data` snapshot; see the middleware's class doc for the full rationale.
>
> **Still open:** the user-facing *per-step* "cancel if [data condition]" / timeout feature.
> That variant *is* a condition over workflow data, so native `.CancelCondition` **should be
> evaluated first** here — it is the right tool for a data-driven step-level cancel, unlike the
> external-state run cancel above.

**WorkflowCore:** `.CancelCondition(data => expression, continueAfterCancellation)` — cancel a running step if a condition becomes true.

**Why it matters:** Enables timeout patterns and external cancellation signals. "If the automation is cancelled by a user, abort this long-running HTTP request." Currently, steps run to completion regardless of external state changes.

**User-facing concept:** "Cancel if" condition on any step, with option to continue or terminate.

**Complexity:** Low-Medium — needs condition evaluation during step execution and clean cancellation propagation.

---

### 9. Activity Workers

**WorkflowCore:** `.Activity(name, keyExpression)` + `GetPendingActivity()` / `SubmitActivitySuccess()` — workflow pauses while an external worker processes a work item and returns a result.

**Current state:** Partially covered by `RequestApprovalAction` using `WaitForEvent`, but that's approval-specific. The generic activity pattern (delegate arbitrary work to an external process) is not exposed.

**Why it matters:** Enables integration with external processing systems. "Send this document to an OCR service, wait for the result." "Queue this for a human review in an external tool."

**User-facing concept:** "Wait for external task" step that publishes a work item and resumes when a result is submitted.

**Complexity:** Medium — the plumbing (WaitForEvent) exists, but needs a generic activity API, worker registration, and timeout handling.

---

### 10. Suspend / Resume / Terminate Workflow ⚠️ Partially addressed

> **Terminate** now works mid-execution: PR #127 observes `TerminateWorkflow`'s result (it
> returns `false` and no-ops when the executor holds the per-workflow lock) and falls back to
> cooperative termination via `RunCancellationStepMiddleware`. **Suspend / Resume remain open.**

**WorkflowCore:** `SuspendWorkflow()`, `ResumeWorkflow()`, `TerminateWorkflow()` — programmatic control over workflow lifecycle.

**Current state:** No API to manually suspend, resume, or terminate a running automation.

**Why it matters:** Operational control. Admins need to pause a misbehaving automation, fix configuration, and resume it — rather than letting it fail or waiting for it to complete. Also needed for graceful shutdown scenarios.

**User-facing concept:** Suspend/Resume/Terminate buttons on the run detail view.

**Complexity:** Low — WorkflowCore supports this natively. Needs management API endpoints and UI controls.

---

### 11. Workflow-Level Middleware

**WorkflowCore:** `IWorkflowMiddleware` — pre/post execution hooks that run before and after the entire workflow.

**Current state:** Custom action-level middleware pipeline (`ActionMiddlewarePipeline`), but nothing at the workflow level.

**Why it matters:** Cross-cutting concerns that apply to the whole run: global timeout enforcement, workspace-level rate limiting, run-level audit logging, or injecting shared context before any step executes.

**User-facing concept:** Not directly user-facing — infrastructure for workspace policies and governance features.

**Complexity:** Low — WorkflowCore supports registration via DI. Needs integration with existing middleware and workspace configuration.

**Caveat — `PostWorkflow` only fires on success:** WorkflowCore's `PostWorkflow` middleware runs **only when a workflow reaches `Complete`** — it does *not* fire on `Terminated`, errored, or `Suspended` (verified against the 3.9.0 source: the post-middleware runner is gated behind `workflow.Status = WorkflowStatus.Complete` in `WorkflowExecutor`). So any *failure-aware* cross-cutting concern (e.g. run-level audit that must capture failed runs) cannot use `PostWorkflow` middleware — hook `AutomationRunCompletedNotification` (published by `RunFinalizer` for **all** terminal outcomes) instead. `PreWorkflow` middleware is unaffected and remains the right tool for pre-run setup (shared-context injection, global timeout arming).

> **The circuit breaker no longer depends on this item.** It was originally scoped as a `PostWorkflow` middleware (and as a forcing function to land #11), but that hits the caveat above — a misconfigured automation that fails every run would never reach the middleware. It now hooks `AutomationRunCompletedNotification` directly. See [circuit-breaker-auto-disable.md](plans/internal/circuit-breaker-auto-disable.md) §1.5.

---

## Suggested Implementation Order

A pragmatic ordering based on user value vs complexity:

| Phase | Feature | Rationale |
|-------|---------|-----------|
| ✅ | ~~ForEach (#2)~~ | **Done** (custom `ForEachContainerStepBody`; perf hardened in #128) |
| ✅ | ~~Parallel execution (#1)~~ | **Done** (custom `ParallelContainerStepBody`) |
| ✅ | ~~While loops (#3)~~ | **Done** (custom `WhileContainerStepBody`) |
| ⚠️ | Terminate (part of #10) | **Done** via cooperative cancel (#127); Suspend/Resume still open |
| ⚠️ | CancelCondition (#8) | External run cancel done (#127); per-step "cancel if" still open |
| **1** | Suspend error mode (#7) | Low effort, completes existing error handling |
| **1** | Workflow-level middleware (#11) | Low effort; good for `PreWorkflow` setup. Narrower than first thought — `PostWorkflow` only fires on success, so it's not a fit for failure-driven governance (the circuit breaker no longer needs it; see #11 caveat) |
| **3** | Saga/Compensation (#6) | High effort, critical for multi-system reliability |
| **4** | Schedule (#4) | Medium effort, enables background fork patterns |
| **4** | Recur (#5) | Medium effort, niche — mostly useful for in-run monitoring |
| **4** | Activity workers (#9) | Medium effort, enables external processing delegation |
