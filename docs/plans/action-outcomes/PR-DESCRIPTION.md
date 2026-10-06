[Plan folder](https://github.com/umbraco/Umbraco.Automate/tree/v18/feature/action-outcomes/docs/plans/action-outcomes)

## Why the change

Actions can now declare named outcomes that the canvas draws as separate exits, so an automation can branch straight off an action's result (for example an AI decision, or Get Content's "Not found") without a follow-up If or Switch step.

## Special things to note

- **Needs a decision:** With error behaviour **Retry**, a step that fails terminally (for example a broken outcome declaration) returns `Next()` and the run carries on, so an "Any result" line from that step still fires. This is existing behaviour for every terminal failure, not new here, but outcomes make it more visible. Should a terminal failure stop "Any result" lines?
- **Needs a decision:** A service account with **root** start nodes can still update or create under content and media that is in the recycle bin (the authorizer allows it and `GetById` returns the trashed node). Scoped accounts now get "Not found" instead. Existing behaviour; a `Trashed` check would make both behave the same.
- **Needs a decision:** Exit tooltips for built-ins are a hard-coded alias table in the canvas (`outcome-exits.ts`). An optional `Description` on `StepOutcome` would let any action supply its own, but it changes the public contract, so it was left out.
- **Behaviour change (changelog):** 8 built-in actions now take their not-found exit instead of failing the step when the item (or parent) no longer exists or is in the recycle bin: Get Content, Get Content Property, Update Content Property, Create Content (parent), Get Media, Get Media Property, Update Media Property, Create Media (parent). Permission failures still fail with the same message. A saved automation that relied on that failure will now continue down its not-found or "Any result" line. Agreed with the product owner during the build.
- **Behaviour change (public test package):** `ActionTestHarness`'s default `IEditableModelResolver` now returns the `WithSettings` object for non-null data (it used to always return null). Null data still returns null, and a resolver registered with `WithService` still wins. Documented on `WithSettings`.
- **Migration:** `UmbracoAutomate_AddStepRunBranchOutcome` adds a nullable, unbounded `BranchOutcome` column to `umbracoAutomateStepRun` for SQL Server and SQLite. `StepRun.BranchOutcome` was set by If/Switch before but never saved; this fixes that too. Applied to an existing SQLite demo DB and to a fresh SQL Server 2022 container. Existing rows read back null.
- **New public API (additive):** `StepOutcome`, outcome members on `IStepType` (default interface members) and `StepTypeBase`, `SettingsResolutionException`, `string.ContainsBinding()`, `AutomationAuthorizationResult.IsNotFound`, `ToFailedActionResult()`, an `AuthorizeContentAsync(Guid, IReadOnlyList<string>, …)` overload, and harness `GetOutcomesAsync` / `ExecuteWithOutcomeAsync` / `RoutedActionResult`. `CatalogueMapDefinition`'s parameterless constructor is kept but `[Obsolete]`.
- **Security fix along the way:** settings-resolution error messages could include a resolved configuration value; they now name the key only. This also covers step run errors and publish validation, which already showed these messages.
- **Consumers:** Umbraco.AI.Automate must raise its Automate minimum to the release that ships this before it overrides the outcome members (plan task T23). The v17 port is in progress on a separate branch.
- **Not in this PR:** If, Switch and Request Approval still use their hard-coded handles (they now record the exit taken, though). `resolveOutputSchema` still resolves a 500 silently; fixing it adds toasts to the binding picker, so it's a follow-up.

## Change outline

An action describes its exits through the step type, the same way it already describes its output schema.

```diff
 IStepType
   GetOutputSchema() / HasDynamicOutputSchema / GetOutputSchemaAsync(settings)
+  GetOutcomes()                         => []      // static list
+  HasDynamicOutcomes                    => false
+  GetOutcomesAsync(savedSettings)       => GetOutcomes()

+record StepOutcome(string Key, string Label) { bool IsDefault }   // label may be "#term"
```

At run time the engine still routes with WorkflowCore's `ValueOutcome`; the step body only maps "no outcome" to the default and checks the declaration.

```diff
 ActionStepBody (success path)
   action.ExecuteAsync(boundSettings)
+  StepOutcomeRouter.ResolveAsync(action, savedSettings, result)   // shared with the test harness
+    static → GetOutcomes(); dynamic → GetOutcomesAsync(saved, unbound settings)
+    invalid declaration / no outcome + no default / list throws → terminal Validation failure
+    no outcome + default → default key; undeclared key → route + run-log warning
+  stepRun.BranchOutcome = routedKey                                 // now persisted
   return ExecutionResult.Outcome(routedKey)                         // WorkflowCore picks the lines
```

Publish blocks lines from outcomes a step no longer has; the catalogue exposes outcomes, and a new endpoint resolves settings-driven ones.

```diff
 AutomationService.ValidateForPublishAsync
   AddDanglingStepReferenceErrors
+  AddDanglingOutcomeErrorsAsync     // "Step 'X' has a connection from outcome 'b', which the step no longer has…"

 Catalogue API
   GET  catalogue/actions                      items += outcomes[], hasDynamicOutcomes
+  POST catalogue/step-types/{alias}/outcomes  → StepOutcome[] | 400 Invalid settings | 404
   POST catalogue/step-types/{alias}/output-schema   + 400 Invalid settings (was an unhandled 500)

 Run API
   StepRunResponseModel                        + branchOutcome: string | null
```

The exit taken is now saved.

```diff
 umbracoAutomateStepRun (
   ...
   LogEntries   nvarchar(max) NULL,
+  BranchOutcome nvarchar(max) NULL      -- SQLite: TEXT NULL
 )
```

On the canvas, an action that declares outcomes draws its exits on the right edge; the run view styles the line each step took.

```diff
 ActionNode
-  single bottom exit
+  declares outcomes?
+    no  → single bottom exit (unchanged)
+    yes → ActionOutcomeExits (right edge)
+            one row per outcome, "+" each, "(default)" marked
+            "Any result" row only while an old unnamed line exists (+ both-paths warning)
+            "Missing outcome: key" (red) for lines on outcomes that no longer exist
+            neutral rows if a dynamic step's outcomes couldn't be loaded

 automation-workflow-workspace-view  #syncFromModel
+  phase 1: render latest model now with last known exits
+  phase 2: resolve dynamic outcomes → re-render from latest model if exits changed

 run-canvas-view
+  outcome-aware lookup; lines styled taken / not taken from branchOutcome
 step-run-detail
+  "Exit taken: <label>"
```

Built-in actions declare their existing outcome keys plus a `success` default (labels in `lang/en.ts`), and those that look items up by key route missing or trashed items to not-found.

```diff
 Core/Actions/BuiltIn/
 ├── GetContent / GetContentProperty / FindContent / CreateContent / UpdateContentProperty / NotifyEditor
 ├── GetMedia / GetMediaProperty / FindMedia / CreateMedia / UpdateMediaProperty
+│     GetOutcomes() => [success (default), ...existing Outcome* keys]
+│     authorizer IsNotFound → SuccessWithOutcome(notFound | parentNotFound)
 Core/Security/
+├── AutomationAuthorizationResult.IsNotFound   // CMS NotFound, or trashed node outside start nodes
+└── AutomationActionAuthorizerExtensions.ToFailedActionResult()
```

Tests: 2,109 unit and 195 integration (Automate), all green; 49 new Playwright specs under `tests/DefaultConfig/Outcomes/`, and the full acceptance suite (106) passes against the demo site.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
