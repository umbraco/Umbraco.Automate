# Architecture

## Extension points

This feature extends **Automate's own step-type contract**. It adds no new Umbraco CMS
extension point. Everything hangs off three seams that already exist:

| Seam | Today | What changes |
| --- | --- | --- |
| `IStepType` / `StepTypeBase<…>` (`Core/StepTypes/`) | Declares settings schema, output schema (static, or dynamic from settings) | Also declares **outcomes**, static or dynamic, using the same shape as output schema |
| Catalogue Management API (`Web/Api/Management/Catalogue/`) | `StepTypeItemResponseModel` + `POST step-types/{alias}/output-schema` | Adds outcomes to the item model, plus `POST step-types/{alias}/outcomes` |
| Canvas (`Web.StaticAssets/Client/.../canvas/`) | `ActionNode` draws one unnamed exit; If/Switch/Approval exits hard-coded by alias | `ActionNode` draws one exit per declared outcome when the action declares any |

**Why here.** Output schema already solved "a step type describes something about itself, which
may depend on its settings, and the editor needs it". The pieces are `GetOutputSchema()`,
`HasDynamicOutputSchema`, `GetOutputSchemaAsync(settings)`, `ResolveStepTypeOutputSchemaController`
and `catalogueRepo.resolveOutputSchema`. Outcomes are the same problem, so they use the same
shape. A developer who has written a dynamic-output action already knows how to write a
dynamic-outcome one.

**Rejected: an `[ActionOutcome("key", "Label")]` attribute.** Attributes can't depend on
settings, and the AI decision action needs settings-driven outcomes. We'd end up with two
mechanisms for one idea.

**Rejected: a separate opt-in `IOutcomeAction` interface.** Each new capability would add a
new interface to probe with `is` checks. Output schema didn't go that way, and two capabilities
with the same shape shouldn't be found in two different ways.

**Rejected: putting it on `IAction` only.** Control flows (If/Switch) are `IStepType` but not
`IAction`. The planned follow-up moves them onto declared outcomes. Putting the members on
`IStepType` makes that follow-up a small change instead of a second API. Triggers get the
empty default and the canvas ignores outcomes on triggers (brief non-goal).

### The contract

```csharp
namespace Umbraco.Automate.Core.StepTypes;

/// One named exit a step can leave through.
public sealed record StepOutcome(string Key, string Label)
{
    /// The outcome taken when the action succeeds without naming one.
    public bool IsDefault { get; init; }
}

public interface IStepType
{
    // …existing members…

    IReadOnlyList<StepOutcome> GetOutcomes() => [];
    bool HasDynamicOutcomes => false;
    Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(
        Dictionary<string, object?>? settings,
        CancellationToken cancellationToken = default)
        => Task.FromResult(GetOutcomes());
}
```

`StepTypeBase` overrides these with virtuals, matching the output-schema members:
`public virtual IReadOnlyList<StepOutcome> GetOutcomes()`, `public virtual bool HasDynamicOutcomes`,
and `protected virtual Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(TSettings? settings, …)`.
The explicit `IStepType.GetOutcomesAsync` resolves typed settings and delegates, just as
`IStepType.GetOutputSchemaAsync` does today.

> ASSUMPTION: Default interface members on `IStepType`, rather than plain abstract members.
> Every known implementer goes through `StepTypeBase` (checked: core, Slack, Umbraco.AI.Automate),
> but `IStepType` is public, so a direct third-party implementer must not break inside a major.
> The defaults cost nothing for `StepTypeBase` users.

**Rules a declaration must follow** (checked by one shared validator, see Key decisions):

- `Key` is non-empty and unique within the step. It is what's stored on
  `StepConnection.Outcome` and `SourceHandle`, so it should be **stable**. Built-in and
  developer-defined keys must never come from a label. An action *may* use author-typed text as
  the key (Pick-One option keys, Ask Score level names). Renaming that text then breaks its
  line, and that's accepted: the line shows as **Missing outcome** on the canvas and publish is
  blocked (decisions 6 and 13), so it's never silent. A hidden stable id is the action's own
  choice to add.
- `Label` is display text only. It follows the settings-field convention
  (`EditableModelSchemaBuilder`): a label starting with `#` is a localization key, and anything
  else is shown as-is. Built-in actions use keys (`#uaOutcomes_<key>`). Labels taken from
  author-typed settings, such as decision options, stay literal.
- Outcomes are computed from the step's **saved, unbound settings**, everywhere: in the
  editor, at publish and at run time. A settings value holding a binding expression
  (`${ … }`) contributes no outcomes, because its real value doesn't exist until the run, and
  you can't draw a line to an exit that doesn't exist yet.
  Bindings are resolved on the *typed* settings after `ResolveSettings` (`ActionStepBody`
  calls `SettingsBindingResolver.ResolveBindings`), so in `GetOutcomesAsync` a bound value
  shows up as raw `${ … }` text in a string property. `BindingTokenizer` is internal, so a
  small public helper, `string.ContainsBinding()` in `Umbraco.Automate.Extensions`, lets
  action authors skip those values.
- **At most one** outcome is `IsDefault`. A default is only needed when the action can
  succeed *without* naming an outcome (the built-ins' "Found"). An action that always names one,
  like the AI decision actions, declares no default, so it gets no exit that can never fire.
- `GetOutcomesAsync` must be cheap and side-effect free. It reads settings only: no network,
  no database. It runs on canvas load, on settings save, at publish, and at run time.

**"Declares outcomes"** means `HasDynamicOutcomes || GetOutcomes().Count > 0`, everywhere this
document and SPEC use the phrase. A dynamic action that resolves to an *empty* list for some
settings still declares outcomes: its named lines are checked at publish and drawn as Missing
outcome, and it keeps the outcome layout (decision 14).

## Data model & persistence

**None.** No entity, no migration, no new column.

- Connections already persist `Outcome` and `SourceHandle` inside the automation definition
  JSON (`AutomationDefinitionDto.Connections`).
- `StepRun.BranchOutcome` already exists and is already persisted. It's set today only by
  If/Switch. This feature also sets it on action step runs.
- SQL Server and SQLite: unaffected.

## Connected systems

| System | Applies? | Why |
| --- | --- | --- |
| Version history (`AutomationVersionableEntityAdapter.CompareConnections`) | No change | Connections are already versioned whole. Moving a line to another exit already shows up as a connection change |
| Import / export (`AutomationExportModel.Connections`) | No change | `StepConnection` is exported as-is. An imported automation with a stale outcome is caught by the publish check below, same as any other step |
| Publish validation (`AutomationService.ValidateForPublishAsync`) | **Yes** | New `AddDanglingOutcomeErrors`, next to `AddDanglingStepReferenceErrors` |
| Run tracking (`StepRun.BranchOutcome`) | **Yes** | Recorded for actions too, so run history shows which exit was taken |
| Run log (`ActionContext.LogEntries`) | **Yes** | A warning when an action returns an outcome it didn't declare |
| WorkflowCore compile (`WorkflowCompiler.WireTransitions`) | **No change** | Already turns `Outcome` into `ValueOutcome`. Unnamed edges keep matching everything (that is how "Any result" keeps working) |
| OpenAPI client (`api/types.gen.ts`, `sdk.gen.ts`) | **Yes** | Regenerate after the catalogue model and new endpoint |
| Action test harness (`Umbraco.Automate.Testing.ActionTestHarness`) | **Yes** | Let action authors assert declared outcomes and default-outcome mapping without a host |
| Startup validation (`AutomationStartupValidator`) | No | It checks saved automations against registered providers. Declaration mistakes are a developer error, caught by the shared validator in tests and at run time |
| Deploy connectors | No | No Deploy connector exists in this repo |
| Search / Examine, cache refreshers, CMS notifications | No | Nothing here is content or cached |
| v17 line | **Yes** | Ported via the Backport Workflow. The touched code is identical on both lines today |

## Key decisions

1. **Reuse WorkflowCore outcome routing as-is.** `ActionStepBody` already returns
   `ExecutionResult.Outcome(key)`, and the compiler already wires `ValueOutcome`s. No engine or
   compiler change. *Rejected:* compiler-generated "else" routing. WorkflowCore's
   `ValueOutcome.Value` is evaluated against workflow data, not the step result, so it can't
   express "if no other edge matched" without custom routing, which "WorkflowCore First" rules out.

2. **A declared default outcome replaces "no outcome".** When an action that declares outcomes
   returns success with no outcome, `ActionStepBody` emits the default outcome's key. This is
   what gives the media/content actions a real "Found" exit that does *not* also fire on
   "Not found". If the action declares outcomes but **no default**, returning no outcome is an
   action bug: the step fails with a terminal `Validation` error saying the action must name
   one of its outcomes. *Rejected:* a reserved magic key like `"default"`. Switch already uses
   that string for its own fallback, and actions should name their own default. *Rejected:* a
   required default (the first draft). Actions that always name an outcome would get an exit
   that can never fire.

3. **Outcomes are resolved in `ActionStepBody`, lazily, not in `WorkflowCompiler`.**
   `Compile` is synchronous and also runs during recovery (`WorkflowDefinitionRecovery`).
   Resolving dynamic outcomes there would mean sync-over-async on every compile. The body is
   already async and only needs the list when the action declares outcomes. It resolves once
   per step execution and only when needed: to map "no outcome" to the default, and to warn on
   an undeclared one. If `GetOutcomesAsync` throws at run time, the step fails with a terminal
   `Validation` error, the same as a broken declaration. There's no sensible exit to guess.

4. **Old unnamed lines keep working, shown as "Any result"** (decided with the user). An
   automation saved before an action declared outcomes has an unnamed line from it. Unnamed
   lines match every outcome in WorkflowCore, so at run time that line keeps firing on every
   result. Nothing saved changes what it does, on v17 or v18. On the canvas, an extra
   **Any result** exit appears on that node *only while such a line exists*. Users can move the
   line to a named exit when they choose to. New unnamed lines can't be drawn from a node that
   declares outcomes. While an "Any result" line **and** at least one named line both leave the
   same node, both fire on that outcome and the run splits into two paths. The node shows a
   warning for as long as that's true (decision 15). *Rejected:* moving old lines to the default exit on save. That quietly
   changes behaviour, since "not found" would stop continuing, inside a major and on an LTS line.

5. **An undeclared outcome is routed, not failed.** If an action returns a key it didn't
   declare, WorkflowCore routes it as normal (usually nothing matches and that path ends, while
   "Any result" lines still fire). A warning goes in the step's run log. *Rejected:* failing the
   step. It turns a developer bug into a run failure for the editor, and today's actions already
   return outcomes nobody declared.

6. **Stale lines block publish.** A line whose `Outcome` isn't in the step's resolved outcomes,
   for example after a decision option was deleted, is a publish error naming the step and the
   missing outcome. It can never fire, so letting it publish only hides the mistake. Saving a
   draft stays allowed, matching how dangling step references are handled today.

7. **One shared declaration validator.** A small internal `StepOutcomeValidator` checks the
   rules in "The contract" (unique non-empty keys, no `__` prefix, at most one default). It's used by
   `ActionStepBody` before mapping (an invalid declaration fails the step with a clear,
   terminal error rather than guessing), by publish validation, and by the test harness.
   *Rejected:* validating at registration. Dynamic outcomes depend on settings, so they can
   only be checked once settings exist.

8. **Built-in actions that already return outcomes declare them in this feature.** There are
   11: `GetContent`, `GetContentProperty`, `FindContent`, `CreateContent`,
   `UpdateContentProperty`, `GetMedia`, `GetMediaProperty`, `FindMedia`, `CreateMedia`,
   `UpdateMediaProperty`, `NotifyEditor`. Each adds a default success outcome plus its existing
   `Outcome*` constants. The constants keep their current values, so returned keys don't change.

   > ASSUMPTION: The default success outcome is keyed `success`, labelled after the action, for
   > example "Found" (Get/Find), "Created" (Create), "Updated" (Update), "Sent" (Notify Editor).

9. **The canvas extends `ActionNode` rather than adding a node type.** `getNodeType` still
   returns `"action"`. `ActionNode` renders a single unnamed exit when the action declares no
   outcomes, and a stacked list of exits (Switch's layout) when it does (see decision 14). Follow-up path: If,
   Switch and Approval become actions or control flows that declare outcomes and render through
   the same component. Then their hard-coded node types and the duplicated
   "must stay in step with…" constants can go. *Rejected:* a new `OutcomeNode` type. That would
   be a fourth hard-coded branching node, which is the pattern this feature exists to retire.

10. **Outcomes come from unbound settings only.** `ActionStepBody` resolves outcomes from
    `_stepConfig.Settings` (the saved dictionary), never from the binding-resolved settings it
    passes to `ExecuteAsync`. The editor and publish check use the same saved settings, so all
    three always see the same list, and a line that is valid in the editor is valid at run
    time. *Rejected:* resolving outcomes from bound settings at run time. The run-time list
    could then differ from the one the lines were drawn against, which would produce stale
    lines that no check can catch before the run. Per-option paths for options that are only
    known at run time are what a Switch after the step is for.

11. **Outcome labels are localizable via the `#` convention.** This matches settings fields,
    so it needs no new mechanism. The canvas shows labels through `localize.string()`.
    *Rejected:* a separate `LabelKey` property. Two fields for one string, unlike every other
    label in the package.

12. **No reserved handle id collides with action keys.** The "Any result" exit uses a
    client-only handle id (`__any__`) that maps to `sourceHandle: null, outcome: null` on save.
    Action keys must not start with `__`, which the shared validator enforces. The whole `__`
   namespace is reserved for exits the system adds, not ones actions declare. That includes the
   error exits planned in `docs/plans/internal/custom-error-paths.md` (see "Fit with error paths").

13. **"Missing outcome" covers renames and empty lists alike.** Any named line whose key isn't in
    the step's current resolved list is drawn on a red Missing outcome exit and blocks publish.
    That's the same whether the option was renamed, deleted, or the dynamic list came back empty.

14. **Layout follows "declares outcomes", not the exit count.** An action that declares no
    outcomes keeps its single exit at the bottom, unchanged. An action that declares outcomes
    always stacks its exits on the right edge, like Switch, even with only one. *Rejected:*
    choosing the layout by exit count. A settings-driven node's count changes as you edit it,
    and "Any result" / "Missing outcome" exits come and go, so the node would jump between
    layouts and drag its lines around. The 11 built-ins all declare two or more outcomes, so
    their exits move from the bottom to the right in existing automations. That's accepted, and
    the Playwright story checks auto-layout spacing still works.

15. **Warn when "Any result" and a named exit both have lines.** It's legal and sometimes
    intended, so it isn't blocked. But on an upgraded automation it's easy to do by accident
    (draw "Found" without moving the old line), so the node shows a warning while both exist.
    *Rejected:* blocking publish. That would break automations that rely on both paths running.

### Fit with error paths

`docs/plans/internal/custom-error-paths.md` adds a *failure* path per step. This feature only
adds *success* outcomes, and the two stay separate:

- Outcomes route only on success. A failed step never takes an outcome exit (SPEC, run-time
  behaviour), so the two can't both fire for one result.
- Error exits, when built, should use a reserved `__`-prefixed handle id (for example `__error`)
  on the same node, rendered after the outcome exits. Actions can never declare `__` keys, so
  there's no collision.
- Error exits are drawn regardless of whether the action declares outcomes, so they must not
  change the layout rule in decision 14. That's for the error-paths design to confirm.

### Consumer contract (for Umbraco.AI.Automate, not built here)

Checked against Umbraco.AI's `v18/feature/decision-capability` branch (`d80c0d14`). Declaring
outcomes is the action's choice. The rule of thumb: **declare outcomes only when one run
produces exactly one answer.** A run leaves through a single outcome (`ExecutionResult.Outcome`
carries one value), so exits only make sense for answers that exclude each other.

| Action | Outcomes | Default | Notes |
| --- | --- | --- | --- |
| **Ask Yes/No** | `true`, `false` (static) | none | The threshold always resolves to true or false |
| **Ask Pick-One** | one per option: `StepOutcome(option.Key, option.Key)` (dynamic) | none | Options are `AskChoiceDecisionOption { Key, Value }`. `Value` is an optional description for the AI and can be long, so it isn't the label. `DecisionAnswerChecker` already rejects a choice that isn't one of the keys |
| **Ask Score** | one per level: `StepOutcome(levelName, levelName)` (dynamic) | none | The answer is a position in `Levels`. The action maps it to the level name. Keyed by name, not position: renaming shows as Missing outcome, while position keys would silently re-route lines when levels are added or reordered. Needs a duplicate-level-name check in Umbraco.AI Core, or Automate's duplicate-key rule will fail the step |
| **Ask Questions** | none | none | Asks several questions and returns one answer per question, so there's no single answer to branch on. Keeps one normal exit and writes every answer to its output |

**Branching on a multi-question action.** Use control flow after the step. The canvas allows
one line per exit, so either chain Switch (or If) steps, one per question, each reading that
question's answer from the output, or follow the step with a Parallel container whose paths
each start with an If or Switch on their own question's answer.

**Bindings.** The options list (and the levels list) as a whole can't be bound to a previous
step. But `SettingsBindingResolver` does resolve string values inside list items, so a single
option key or level name *can* hold a `${ … }` binding. Such an entry contributes no exit
(`ContainsBinding()`). In every case the model's raw answer stays in the step output for later
steps to bind to. The outcome only picks the exit.

**Renames.** Pick-One keys and Score level names are typed by the author, so renaming one breaks
its line. That's accepted (see "Rules a declaration must follow"). A hidden stable id in the
Umbraco.AI property editor is optional.

**Version floor.** Umbraco.AI.Automate currently allows Automate `[18.0.0, 18.999.999)`. Once it
overrides the outcome members, its minimum must be the Automate release that ships outcomes, on
both the v17 and v18 lines. Its overrides don't exist on an older Automate's `StepTypeBase`,
so loading it there would fail with a type-load error rather than quietly degrade. Tracked as
PLAN T23.
