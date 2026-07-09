# Scheduled trigger output is never populated (fixes #152)

## Context

While implementing #84 (allow manual "Run now" for Scheduled-trigger automations), we discovered — and then empirically confirmed against the running demo site — that `ScheduledTriggerOutput` (`FiredAtUtc`, `CronExpression`) is never populated on **any** Scheduled-trigger fire, real or manual. `{{trigger.firedAtUtc}}` / `{{trigger.cronExpression}}` bindings on a Scheduled-trigger automation always resolve to an empty string.

This was filed as [#152](https://github.com/umbraco/Umbraco.Automate/issues/152) with proof: three consecutive real CRON fires (`*/2 * * * *`, `Precise` timing, zero jitter) each logging an empty `firedAtUtc`/`cronExpression`, even though the background job's own diagnostic log line on the same tick shows it has the correct CRON expression in hand at dispatch time.

The intended outcome: a Scheduled-trigger automation's steps can reliably bind to `{{trigger.firedAtUtc}}` and `{{trigger.cronExpression}}`, for both a genuine CRON-driven fire and a manual "Run now" invocation (#84), with equivalent output shape between the two so neither behaves like the "broken" path relative to the other.

## Root cause

`ScheduledTriggerBackgroundJob.PerformExecuteAsync` (`Umbraco.Automate.Core/Triggers/Scheduling/ScheduledTriggerBackgroundJob.cs:168-173`) dispatches a bare `TriggerEvent` with no output payload:

```csharp
var triggerEvent = new TriggerEvent
{
    TriggerAlias = automation.Trigger.TriggerAlias,
    InitiatorType = TriggerInitiatorType.Scheduled,
    IdempotencyKey = $"scheduled:{automationId}:{nextOccurrence:O}",
};
```

Every other built-in trigger (`ContentPublishedTrigger`, `WebhookTrigger`, etc.) instead yields a `TriggerEvent<TOutput>` — a generic subclass carrying a strongly-typed `Output`. `OutboxTriggerDispatcher` already knows how to serialize that output automatically via the internal `ITriggerEventWithOutput` interface (`OutboxTriggerDispatcher.cs:44`). `ScheduledTriggerBackgroundJob` is the only trigger dispatch site in the codebase that doesn't use this — it's simply never been wired up, even though `ScheduledTriggerOutput` has existed as a type the whole time (confirmed via a repo-wide search: it's never instantiated anywhere).

Separately, `TriggerAutomationController.TriggerAutomation` (the "Run now" endpoint added in #84) always passes `triggerOutputData: null` regardless of trigger type — reasonable before this fix (it matched what a real scheduled fire produced), but it's now the odd one out once real fires start producing real output.

## Design

Two small, independent changes — no changes needed to `OutboxTriggerDispatcher`, `TriggerEventHandler`, or `AutomationExecutor`; the generic output-dispatch and output-binding infrastructure already exists and already works correctly for every other trigger type.

**PR split:** this PR implements only change 1 (the real-fire path) — it's the core #152 defect and stands alone. Change 2 (manual "Run now" parity) is deferred to a separate, follow-up PR/branch, since it's really in service of #84's manual-run feature rather than #152's core defect. The two are described together here because they share root cause and design reasoning, and the follow-up PR benefits directly from this one landing first (it reuses the exact same `ScheduledTriggerOutput` shape and settings-resolution approach) — but they ship independently.

### 1. Real scheduled fires — `ScheduledTriggerBackgroundJob.cs`

Change the bare `TriggerEvent` to `TriggerEvent<ScheduledTriggerOutput>`, matching the exact pattern already used by `ContentPublishedTrigger.MapEvent` and friends:

```csharp
var triggerEvent = new TriggerEvent<ScheduledTriggerOutput>
{
    TriggerAlias = automation.Trigger.TriggerAlias,
    InitiatorType = TriggerInitiatorType.Scheduled,
    IdempotencyKey = $"scheduled:{automationId}:{nextOccurrence:O}",
    Output = new ScheduledTriggerOutput
    {
        FiredAtUtc = dueAt,
        CronExpression = cronExpression,
    },
};
```

`FiredAtUtc` uses `dueAt` (the logical scheduled instant, already computed a few lines above as `nextOccurrence + jitterOffset`) rather than `now` (the polling loop's wall-clock instant, which can lag the tick by up to `PollInterval`). This matches the existing idempotency-key design, which is already keyed on `nextOccurrence` rather than wall-clock processing time — "when the schedule says this fired," not "when we happened to notice."

### 2. Manual "Run now" — `TriggerAutomationController.cs` (follow-up PR, not this one)

Inject `TriggerCollection` (already a standard DI-registered collection used elsewhere, e.g. by `ScheduledTriggerBackgroundJob` and `TriggerEventHandler`). Before calling `_executor.ExecuteAsync`, resolve the automation's trigger:

```csharp
var trigger = _triggers.GetByAlias(automation.Trigger?.TriggerAlias ?? "");

Dictionary<string, object?>? triggerOutputData = null;
if (trigger is IScheduledTrigger scheduledTrigger && trigger.OutputType == typeof(ScheduledTriggerOutput))
{
    var settings = trigger.SettingsType is not null && automation.Trigger!.Settings.Count > 0
        ? trigger.ResolveSettings(automation.Trigger.Settings)
        : null;

    var output = new ScheduledTriggerOutput
    {
        FiredAtUtc = DateTime.UtcNow,
        CronExpression = scheduledTrigger.GetCronExpression(settings),
    };

    var json = JsonSerializer.Serialize(output, Core.Dispatch.JsonOptions.Default);
    triggerOutputData = Core.Dispatch.JsonOptions.DeserializeToUnwrappedDictionary(json);
}
```

`FiredAtUtc = DateTime.UtcNow` here (the actual click instant) — there's no "scheduled occurrence" for an ad-hoc manual run, so the honest answer is "now."

The serialize-then-`DeserializeToUnwrappedDictionary` round-trip reuses the exact helper `ReplayRunController` already calls directly from the Web layer today (`Core.Dispatch.JsonOptions.DeserializeToUnwrappedDictionary`, `ReplayRunController.cs:100`) — this is an established, already-accepted pattern in this codebase for a Web controller to use, not a new layering violation.

The `trigger.OutputType == typeof(ScheduledTriggerOutput)` guard keeps this scoped to the concrete built-in `ScheduledTrigger` (the only existing `IScheduledTrigger` implementer) rather than assuming every possible future custom `IScheduledTrigger` shares this exact output shape — avoids speculative generality for a hypothetical trigger type that doesn't exist yet. Any other trigger type (`ManualTrigger`, `WebhookTrigger`, content triggers) keeps `triggerOutputData: null`, unchanged.

## Out of scope (for this PR)

- No change to `ScheduledTriggerOutput` itself, `OutboxTriggerDispatcher`, `TriggerEventHandler`, or `AutomationExecutor` — the generic output-dispatch/binding path already works.
- Change 2 above (manual "Run now" output parity) — implemented and verified during design, but deliberately held back for its own PR/branch rather than bundled here. When that follow-up lands, it will need no generic "any `IScheduledTrigger` gets manual-run output synthesis" infrastructure either — deferred until a second `IScheduledTrigger` implementer actually exists.
- Bundling this with #84 (the frontend "Run now" visibility fix) — kept as a separate branch/PR per explicit instruction, since it's a distinct pre-existing defect rather than something #84 introduced.

## Verification

Manual, using the already-running demo site (`demos/v17/Umbraco.Automate.DemoSite`) and the test automation already set up during #84's investigation (Scheduled trigger, CRON `*/2 * * * *`, Precise timing, Log Message step bound to `${trigger.firedAtUtc} CRON: ${trigger.cronExpression}`):

1. `dotnet build` the solution — confirms no compile errors.
2. `dotnet test` — added `PerformExecuteAsync_ScheduledTriggerDue_PopulatesOutputWithFiredAtUtcAndCronExpression` to the existing `ScheduledTriggerBackgroundJobTests.cs`, following that file's established capture-and-assert pattern. Full `Umbraco.Automate.Tests.Unit` suite passes (1164/1164), no regressions from the `TriggerEvent` → `TriggerEvent<ScheduledTriggerOutput>` type change.
3. Restart the demo site; observed real CRON fires (every 2 minutes) in the log and confirmed `firedAtUtc`/`cronExpression` are now populated with real values instead of empty strings (e.g. `Scheduled run fired at: 09/07/2026 11:18:00 CRON: */2 * * * *`).

Change 2 (manual "Run now" parity) was implemented and verified against the same running demo site during design — confirmed it produces the same populated shape (`firedAtUtc` = click time, correct `cronExpression`) — before being deliberately held back for its own PR. That verification will be repeated/referenced in the follow-up PR.
