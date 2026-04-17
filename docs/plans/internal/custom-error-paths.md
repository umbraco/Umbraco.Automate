# Custom Error Paths Per Step

## Context

Today, when a step fails, the user picks from four fixed strategies: Retry, Suspend, Terminate, or Compensate. There's no way to define *what happens* on failure beyond these — no fallback steps, no "on error, notify someone and continue", no branching based on the type of failure.

Lars Skjold Iversen described their primary use case: a GET request step fails to find expected data. Without custom error paths, the default retry logic kicks in and keeps trying for hours before anyone is notified. What they want is: on failure, immediately stop retrying, notify a human, and terminate cleanly.

This is Zapier's most-loved error handling feature. Users activate it on individual steps and attach an alternate sequence of steps that runs when the parent step fails.

### What we have today

- `StepErrorBehavior` enum: Retry, Suspend, Terminate, Compensate
- `StepConfiguration` has `ErrorBehavior`, `MaxRetries`, `RetryInterval` per step
- `IStepErrorClassifier` classifies errors as terminal vs transient
- `ActionStepBody.DecideFailureOutcome()` maps behavior to WorkflowCore outcomes
- `WorkflowCompiler.ApplyErrorBehavior()` wires error handling at compile time, casting `StepErrorBehavior` to `WorkflowErrorHandling`
- WorkflowCore supports Compensate (saga pattern) with `CompensateWith<T>()` on steps
- The automation model stores steps as a flat list in `StepConfiguration[]`

### What's missing

- No user-defined error steps — only predefined strategies
- No way to run alternate logic on failure (notify, log, call a different API)
- No error context passed to fallback steps (what failed, why, which step)
- The Compensate behavior exists but has no UI and is limited to a single compensation step type

---

## Design

### Approach: New `ErrorPath` behavior with child steps

Add a new `StepErrorBehavior.ErrorPath` that tells the compiler to attach a sequence of user-defined steps that execute when the parent step fails (after retries are exhausted or on terminal error).

This builds on WorkflowCore's existing `CompensateWith` infrastructure but repurposes it for user-defined error handling rather than saga rollback.

### Relationship to WorkflowCore features

**ErrorPath vs Saga/Compensation (`workflowcore-feature-gaps.md` #6):**

Both use WorkflowCore's `CompensateWith<T>()` under the hood, but they serve different purposes and should be surfaced as distinct features in the UI:

| Feature | Purpose | When to use |
|---------|---------|-------------|
| **ErrorPath** (this spec) | Forward-looking fallback: "if step X fails, do Y instead" | Notify a human, log to external system, graceful degradation |
| **Saga/Compensation** (#6) | Backward-looking rollback: "undo the work that prior successful steps did" | Multi-system transactions where partial failure must be reverted |

Implementation implication: if Saga lands, we need to ensure a step can't have both an ErrorPath and a Compensation — or define clear semantics for which runs first. Recommend: **ErrorPath takes precedence**. If defined, Saga compensation doesn't run for that step (the error path is the explicit handler).

**ErrorPath + Suspend error mode (`workflowcore-feature-gaps.md` #7):**

The WorkflowCore gaps doc flags "Suspend error mode" as a Phase 1 low-effort feature. Once implemented, we can offer Suspend as an outcome after error path completion alongside Terminate and Continue:

```csharp
public enum ErrorPathOutcome
{
    Terminate = 0,   // Default — stop the run after error path completes
    Continue = 1,    // Continue to the next step after the failed step
    Suspend = 2,     // Pause the run for manual intervention
}

public ErrorPathOutcome Outcome { get; set; } = ErrorPathOutcome.Terminate;
```

This replaces the `ContinueAfterErrorPath` boolean (see §1.7) with a more expressive enum. Suspend is particularly valuable for Lars's use case: run the error path (notify a human, log context), then suspend so the human can inspect the run state and decide whether to resume or terminate.

### Phase 1: Backend — error path execution

#### 1.1 New `StepErrorBehavior` value

```csharp
public enum StepErrorBehavior
{
    Retry = 0,
    Suspend = 1,
    Terminate = 2,
    Compensate = 3,
    ErrorPath = 4,
}
```

#### 1.2 Extended `StepConfiguration`

Add an `ErrorSteps` property to hold the error path:

```csharp
public sealed class StepConfiguration
{
    // ... existing properties ...

    /// <summary>
    /// Gets or sets the steps to execute when this step fails and
    /// <see cref="ErrorBehavior"/> is <see cref="StepErrorBehavior.ErrorPath"/>.
    /// Executed in order after the step fails (post-retry if retries are configured).
    /// </summary>
    public List<ErrorStepConfiguration>? ErrorSteps { get; set; }
}
```

#### 1.3 New `ErrorStepConfiguration`

A lightweight configuration for error path steps. Reuses the same action system but with error context available as inputs:

```csharp
/// <summary>
/// Configuration for a step within an error path.
/// </summary>
public sealed class ErrorStepConfiguration
{
    /// <summary>
    /// Unique identifier for this error step within the error path.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Alias of the action to execute.
    /// </summary>
    public required string ActionAlias { get; init; }

    /// <summary>
    /// User-defined label for the error step.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Optional connection ID for the error step.
    /// </summary>
    public Guid? ConnectionId { get; set; }

    /// <summary>
    /// Step settings as key-value pairs.
    /// </summary>
    public Dictionary<string, object?> Settings { get; set; } = [];

    /// <summary>
    /// Input binding expressions.
    /// </summary>
    public Dictionary<string, string> InputMappings { get; set; } = [];
}
```

#### 1.4 Error context available to error steps

When an error path executes, the failing step's error information should be available as bindable data. Add an `ErrorContext` output that error path steps can reference in their input bindings:

```csharp
/// <summary>
/// Error context available to error path steps via bindings.
/// Accessible as <c>{{error.Message}}</c>, <c>{{error.StepName}}</c>, etc.
/// </summary>
public sealed class StepErrorContext
{
    /// <summary>The error message from the failed step.</summary>
    public required string Message { get; init; }

    /// <summary>The error category classification.</summary>
    public required StepRunErrorCategory Category { get; init; }

    /// <summary>The name/alias of the step that failed.</summary>
    public required string StepName { get; init; }

    /// <summary>The step ID that failed.</summary>
    public required string StepId { get; init; }

    /// <summary>The action alias of the failed step.</summary>
    public required string ActionAlias { get; init; }

    /// <summary>Number of retry attempts before the error path was invoked.</summary>
    public required int RetryCount { get; init; }
}
```

This should be injected into the workflow data context before the error path steps execute, accessible via the standard binding syntax (e.g., `{{error.Message}}`).

#### 1.5 WorkflowCompiler changes

In `WorkflowCompiler.ApplyErrorBehavior()`, when `ErrorBehavior == ErrorPath`:

1. Set the step's WorkflowCore error handling to `Compensate`
2. Compile the `ErrorSteps` list as a compensation chain using WorkflowCore's `CompensateWith<ActionStepBody>()` API
3. Each error step in the chain gets the `StepErrorContext` injected into its input data

```csharp
// Pseudocode for the compiler
case StepErrorBehavior.ErrorPath:
    stepBuilder.OnError(WorkflowErrorHandling.Compensate);
    foreach (var errorStep in config.ErrorSteps ?? [])
    {
        stepBuilder.CompensateWith<ActionStepBody>(
            compensate => ConfigureErrorStep(compensate, errorStep, parentStepConfig));
    }
    break;
```

#### 1.6 Error step run tracking

Error path step executions should be tracked as `StepRun` records on the parent `AutomationRun`, with a flag or convention to distinguish them from normal step runs:

- Option A: Add `IsErrorPathStep` bool to `StepRun`
- Option B: Use a naming convention for the `StepId` (e.g., prefix with `error:`)
- Option C: Add `ParentStepId` nullable field to `StepRun` linking to the failed step

**Recommend Option C** — it's the most flexible and allows the UI to show error path steps nested under their parent.

#### 1.7 What happens after the error path completes

The user chooses the outcome after error path completion via `ErrorPathOutcome`:

```csharp
public ErrorPathOutcome Outcome { get; set; } = ErrorPathOutcome.Terminate;
```

- **Terminate** (default) — stop the run. Matches Lars's primary use case: "notify a human, then stop."
- **Continue** — proceed to the next step. Matches Zapier's default behavior.
- **Suspend** — pause the run for manual intervention (depends on WorkflowCore Suspend error mode landing first).

Default to `Terminate` since it's the safer choice — a step that failed badly enough to hit the error path usually shouldn't continue on the happy path. Users who want to continue opt in explicitly.

### Phase 2: Management API

#### 2.1 Automation CRUD

The existing automation create/update endpoints already accept the full automation definition including steps. `ErrorSteps` on `StepConfiguration` will serialize naturally as part of the existing JSON structure. No new endpoints needed.

#### 2.2 Run detail response

Include error path step runs in the run detail response, nested under their parent step using the `ParentStepId` relationship.

### Phase 3: Frontend

#### 3.1 Step settings panel

When `ErrorBehavior` is set to `ErrorPath`, show an "Error Steps" section in the step settings panel. This is a simplified step list (no canvas, just a vertical list) where the user can:

- Add error steps from the action catalogue
- Configure each error step's settings and input bindings
- Reorder error steps via drag-and-drop
- Reference `{{error.Message}}`, `{{error.Category}}`, etc. in bindings

#### 3.2 Canvas visualization

On the automation canvas, steps with error paths should have a visual indicator (e.g., a small error icon badge). Clicking it expands the error path configuration.

#### 3.3 Run viewer

In the run detail view, error path step runs should be visually nested under the failed parent step with distinct styling (e.g., red-tinted background) to distinguish them from the normal flow.

---

## Migration

- No new tables — `ErrorSteps` is stored as part of the `StepConfiguration` JSON within the `Automation` entity
- New nullable column on `StepRun`: `ParentStepId` (string, nullable) for error path tracking
- Migration prefix: `UmbracoAutomate_`

---

## Testing

### Unit tests

- `WorkflowCompilerTests`:
  - ErrorPath behavior compiles error steps as compensation chain
  - Error steps receive StepErrorContext in input data
  - Empty ErrorSteps list with ErrorPath behavior degrades gracefully (treat as Terminate)
  - ContinueAfterErrorPath = true continues, false terminates

- `ActionStepBodyTests`:
  - ErrorPath behavior triggers compensation flow on failure
  - Terminal errors invoke error path without retrying
  - Transient errors exhaust retries before invoking error path

- `ErrorStepConfigurationTests`:
  - Serialization/deserialization round-trips correctly
  - Input bindings with error context expressions resolve

### Integration tests

- Full flow: automation with error path on a step, step fails, error path executes, run completes with error path step runs tracked
- Error path step itself fails — should not cause infinite recursion (error path steps should NOT have their own error paths)

---

## Open questions

1. **Can error path steps have their own error paths?** No — to prevent recursion. Error path steps use `Terminate` on failure. This is a hard constraint.
2. **Should error paths support the full step canvas or just a linear list?** Start with a linear list (Phase 1). A visual error path branch on the canvas is Phase 3.
3. **Should we rename `Compensate` to `ErrorPath` and deprecate the old value?** No — Compensate has saga semantics (rollback). ErrorPath is forward-looking (handle and continue/stop). They serve different purposes even though both use WorkflowCore's compensation infrastructure.
4. **Should error path steps have access to the failed step's outputs?** If the step partially succeeded and produced output before failing, yes. Pass the last known output data alongside the error context. If no output was produced, the bindings resolve to null.
5. **What actions make sense in error paths?** All existing actions should work — LogMessage, HttpRequest (to notify an external system), Slack messages, etc. No need to restrict the action catalogue.
