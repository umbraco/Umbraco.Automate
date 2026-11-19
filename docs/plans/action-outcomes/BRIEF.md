# Brief

## Problem

**What.** An action can only leave a step through one unnamed exit on the canvas. Anything that
needs to branch on what an action produced has to be followed by a separate If or Switch step
that re-reads the action's output and forks on it. Two steps do the work of one, and the
automation reads worse for it.

**Concrete trigger.** In Umbraco.AI, a new kind of AI model is aimed at making decisions: it
returns either a yes/no answer or one option picked from a list. Today an automation author has
to wire *AI decision → Switch (on `steps.decide.output.choice`) → branches*. The natural shape
is *AI decision → one exit per option*.

**What happens today without it.**

- Authors add an If/Switch after the action and write conditions that copy the action's own
  options by hand. When an option is renamed or added in the action, the Switch has to be
  edited too, and nothing warns if they drift apart.
- Some built-in actions already return named outcomes at runtime (`CreateMediaAction`
  `parentNotFound` / `mediaTypeNotFound` / `fileDownloadFailed`, `GetContentAction` /
  `GetMediaAction` / `FindMediaAction` / `UpdateMediaPropertyAction` `notFound`,
  `UpdateMediaPropertyAction` `propertyNotFound`). There is no way to draw a line from those
  outcomes, so they can't be acted on.

**Who it's for.**

- *Automation authors* (backoffice users building automations on the canvas): they see and
  connect one exit per outcome.
- *Action developers* (package authors writing actions, e.g. Umbraco.AI.Automate, the Slack
  add-on, third parties): they declare which outcomes their action can return, either a fixed
  list or one built from the step's settings.
- Not for: front-end site visitors; people reading run history (they benefit, since the run
  already records `BranchOutcome`, but nothing new is built for them here).

**Why now.** Umbraco.AI's decision-style actions are being built now. If outcomes aren't a
first-class action feature, those actions ship with the If/Switch workaround baked into
docs and examples, and changing it later is a breaking change in how people build automations.

> ASSUMPTION: The AI decision action itself is built in Umbraco.AI.Automate, not in this repo.
> This feature delivers the core capability it needs; the AI action is its first consumer and
> acceptance test.

**What already exists (checked, not assumed).**

- WorkflowCore routes on named outcomes natively: `ExecutionResult.Outcome(value)` plus
  `ValueOutcome` on the step, and it follows *every* matching outcome
  (`ExecutionResultProcessor`: `foreach (var outcomeTarget in step.Outcomes.Where(x => x.Matches(...)))`).
  No custom engine work is needed. Source: workflow-core `master`, `ExecutionResultProcessor.cs`
  and `Models/ValueOutcome.cs`.
- Actions can already return an outcome: `ActionResult.SuccessWithOutcome` /
  `ActionBase.SuccessWithOutcome` (`Actions/ActionBase.cs:34`), turned into
  `ExecutionResult.Outcome` in `ActionStepBody.cs:280`.
- Connections already store the outcome they belong to (`StepConnection.Outcome`,
  `StepConnection.SourceHandle`), and `WorkflowCompiler.WireTransitions` already wires them
  into `ValueOutcome`s, filters included (`WorkflowCompiler.cs:334`).
- If, Switch and Request Approval prove the end-to-end path works, but their exits are
  hard-coded by alias in the canvas (`canvas/utils/model-to-flow.ts`, `getNodeType`) and
  duplicated as string constants on both sides ("Must stay in step with…").

**The real gap.** Actions have no way to *declare* their outcomes, so the canvas can't draw
exits for them.

**Success looks like.**

- An action developer can declare outcomes for an action without touching any frontend code,
  and the canvas shows one labelled exit per outcome.
- Outcomes can depend on the step's settings (e.g. one exit per option the author typed into
  the decision step), and the exits update when the settings change.
- An automation of *AI decision → three branches* runs the right branch, with no If/Switch in
  between, and the run view highlights the branch taken.
- The existing media/content `notFound`-style outcomes become connectable.
- Existing automations (including ones using If/Switch/Approval) open, run and save unchanged.

> ASSUMPTION: "Measurable" here means the acceptance automations above pass end to end in the
> demo site, rather than a usage metric. There's no telemetry to measure adoption against.

**Constraints.**

- Must ship on both active lines: Umbraco CMS 18 (`v18/dev`, STS) and Umbraco CMS 17
  (`v17/dev`, LTS). Both are in the "features + bug fixes" phase (`CONTRIBUTING.md`). Build on
  `v18/dev` first, then port to `v17/dev` through the Backport Workflow. The lines are kept
  separate and never forward-merged.
- The design must not use anything that only exists in CMS 18. Today the code this touches is
  identical on both lines (`WorkflowCompiler.cs`, `ActionResult.cs`, and the whole `canvas/`
  folder show no diff between `v18/dev` and `origin/v17/dev`), and both pin WorkflowCore 3.9.0.
  So the port should be close to a straight copy, as long as the design keeps it that way.
- Must reuse WorkflowCore's outcome routing (root `CLAUDE.md` "WorkflowCore First"). Nothing
  here justifies a custom routing mechanism.
- Public action API changes must stay backwards compatible within each major (v17 and v18): existing
  actions that declare nothing keep their single exit and behave exactly as today.

**Riskiest unknowns.**

- **Unnamed edges always fire.** WorkflowCore's `ValueOutcome.Matches` returns true when the
  edge has no value, so a plain line from a step fires even when the action returns a named
  outcome. Mixing a plain exit with named exits on one step would run both paths. The design has
  to decide what a "default/other" exit means and how the canvas stops the accidental mix.
  This is the most likely source of surprising behaviour.
- **Settings-driven outcomes going stale.** If an author renames or removes an option, any
  line saved against the old name points at an outcome that can never fire. Today that fails
  silently. Outcomes probably need a stable key separate from their label.
- **Outcomes an action returns but didn't declare.** What should happen at runtime, and should
  the editor or publish step warn about it?

> ASSUMPTION: "Multiple outputs" means multiple *exits* (control flow), not multiple separate
> output *data* objects. The action still produces one output object that later steps bind to;
> the outcome just picks which exit(s) to follow.

**Smallest shippable slice.**

> ASSUMPTION: Fixed and settings-driven outcomes on ordinary actions, a canvas node that draws
> them, and a fallback exit for the unmatched case. Moving If/Switch/Approval onto the same
> mechanism is a follow-up, not part of the first slice: it's a refactor with no new
> user-facing value, and it can wait until the new mechanism has proved itself.

**What would kill it.** TODO: only you can answer this. Example candidates: Umbraco.AI decides
decision actions don't need branching after all, or the "unnamed edges always fire" behaviour
can't be made safe without changing how existing automations run.

## Non-goals

- **Moving If, Switch and Request Approval onto declared outcomes.** Desirable cleanup that
  would remove the duplicated constants, but it changes working features for no user gain. Later.
- **Error exits ("on failure, go here").** Covered by the separate custom-error-paths plan
  (`docs/plans/internal/custom-error-paths.md`). Outcomes here are *success* results only. The
  two should not grow two different exit systems, and the design phase should check they fit
  together.
- **Building the AI decision action.** That lives in Umbraco.AI.Automate.
- **Multiple output data objects per action.** One output object per step stays the model.
- **Changing how WorkflowCore picks routes**, e.g. "first match only" instead of "all matches".
  Fork-on-every-match stays as the engine defines it.
- **Triggers with multiple exits.** Triggers keep their single exit.
