# Spec

## Management API surface

All endpoints sit under the existing catalogue controller base (`CatalogueControllerBase`) and
inherit its Automate-section authorization. Nothing new is reachable from the public site.

### Catalogue item: outcomes added

`StepTypeItemResponseModel` (returned by the existing actions / control-flows / triggers
catalogue endpoints) gains:

| Field | Type | Behaviour |
| --- | --- | --- |
| `outcomes` | `StepOutcomeResponseModel[]` | The static outcomes (`GetOutcomes()`). Empty array when none are declared. Never null |
| `hasDynamicOutcomes` | `bool` | `true` when outcomes depend on settings and must be resolved per step |

`StepOutcomeResponseModel`: `{ key: string, label: string, isDefault: bool }`.

- An action that declares nothing returns `outcomes: []`, `hasDynamicOutcomes: false`. Its
  catalogue response is otherwise byte-for-byte what it is today.
- Triggers always return `outcomes: []`, `hasDynamicOutcomes: false`.
- `outcomes` keeps declaration order. The canvas renders exits in this order.

### `POST /step-types/{alias}/outcomes` (new)

Mirrors `POST /step-types/{alias}/output-schema`.

- **Request:** `ResolveOutcomesRequestModel { settings: Dictionary<string, object?> }`.
- **200:** `StepOutcomeResponseModel[]`, in declaration order.
  - Works for every step type. One with static or no outcomes returns its static list (possibly
    empty), so the client never has to special-case.
  - Empty settings resolve as "no settings", the same as output schema
    (`StepTypeBase.IStepType.GetOutputSchemaAsync`).
- **400:** `ProblemDetails` "Invalid settings" when `ResolveSettings` rejects the settings
  (`EditableModelResolver` throws `InvalidOperationException`). The detail is the resolver's
  message. The existing `POST /step-types/{alias}/output-schema` gets the same 400 handling,
  since today the exception goes unhandled.
- **404:** `ProblemDetails` "Step type not found" when no action, control flow or trigger has
  that alias.
- Never calls out to external services. `GetOutcomesAsync` is settings-only by contract.
- Settings are used exactly as sent. Binding expressions are not resolved. A setting whose
  value is a binding expression contributes no outcomes.
- `label` is returned raw (a `#key` or literal text). Translating it is the client's job.

### Publish: new validation error

`POST` publish on an automation fails with the existing `AutomationValidationException` /
problem-details shape when any connection has a non-null `outcome` that isn't a key in its
source step's resolved outcomes, **and** that step's action declares outcomes.

- Message: `Step '<step name>' has a connection from outcome '<key>', which the step no longer
  has. Reconnect or remove it.`
- One error per stale connection.
- Connections from If/Switch/Approval (which don't declare outcomes yet) are not checked by
  this rule. Their behaviour is unchanged.
- Unnamed connections (`outcome: null`) are never flagged. They are the "Any result" lines.
- Saving a draft with a stale connection still succeeds.

### Run-time behaviour (observable through runs)

For a step whose action declares outcomes (always resolved from the step's saved, unbound
settings, so the list matches what the editor showed):

- Success with no outcome → the run follows the default outcome's exit, plus any "Any result"
  line. `StepRun.branchOutcome` is the default key.
- Success with a declared outcome `k` → follows the `k` exit plus any "Any result" line.
  `branchOutcome = k`.
- Success with an undeclared outcome `k` → routes on `k` as today (normally only "Any result"
  lines fire). `branchOutcome = k`. The step's run log has a warning:
  `Action returned outcome '<k>', which it does not declare.`
- A declaration that breaks the rules (duplicate/empty key, a key starting with `__`, zero or
  several defaults) → the step fails with an error naming the action and the broken rule,
  categorised `StepRunErrorCategory.Validation` so the classifier treats it as terminal and
  doesn't retry.

  > ASSUMPTION: `Validation` is already classed as terminal by `IStepErrorClassifier`. Confirm
  > during build, since this assumes the current classifier rules.
- Failure → unchanged (error behaviour as configured). No outcome routing.

For a step whose action declares nothing: identical to today, except `branchOutcome` is now
recorded when the action returns an outcome.

### Built-in actions

The 11 built-in actions listed in ARCHITECTURE decision 8 return the same outcome keys they
return today, plus they now declare them, with `success` as the default. Their labels are
`#uaOutcomes_<key>` keys, with English terms added to `lang/en.ts`. A saved automation
using any of them runs exactly as before. See "Any result" below.

### Test harness

`ActionTestHarness<TAction>` exposes:

- the action's resolved outcomes for given settings;
- the effective branch outcome of an execution (the default key when the action returned none).

so an action author can assert both without a running site.

## Frontend components

All changes live in `Umbraco.Automate.Web.StaticAssets/Client/src/`. No new package, no new
extension type, no new modal.

### Catalogue (`catalogue/type-mapper.ts`, `catalogue/repository/catalogue.repository.ts`)

- The mapped catalogue item carries `outcomes` and `hasDynamicOutcomes`.
- The repository gains `resolveOutcomes(alias, settings)`, the same shape as `resolveOutputSchema`.

### Canvas data (`canvas/types.ts`, `canvas/utils/model-to-flow.ts`, `flow-to-model.ts`)

- `CatalogueLookupEntry` and `ActionNodeData` gain `outcomes?: { key, label, isDefault }[]`.
- On load, each step whose action has `hasDynamicOutcomes` gets its outcomes from
  `resolveOutcomes` with its saved settings. Others use the catalogue's static list.

  > ASSUMPTION: One request per dynamic step, as output schema does today. Batch later only if
  > large automations show it's slow.

- `getContinuationSourceHandle` returns the **default** outcome's key for an action that
  declares outcomes (so "insert step between" continues down the default exit), and `null`
  otherwise, as today.
- A saved connection with `sourceHandle: null, outcome: null` from a step that declares
  outcomes is shown on the `__any__` handle. On save it goes back to
  `sourceHandle: null, outcome: null`. Round-tripping a loaded automation without edits
  produces an identical definition.

### `ActionNode` (`canvas/nodes/ActionNode.tsx`)

When the node's action declares **no** outcomes, it renders exactly as today: one unnamed
bottom exit.

When it declares outcomes, it renders:

- One labelled exit per outcome, in declaration order, stacked like Switch's cases, each with
  its own "+" add-step button. The default outcome is visibly marked (for example "(default)"
  after its label).
- An **Any result** exit, after the declared ones, **only if** an unnamed line currently leaves
  this node. It has no "+" button, so new "Any result" lines can't be drawn. Its tooltip says
  this line runs whatever the step returns, and suggests moving it to a named exit.
- A **Missing outcome** exit, in error styling, for each connected line whose outcome isn't in
  the current list. It's labelled with the stale key and has a tooltip saying the outcome no
  longer exists and the automation won't publish until the line is moved or removed.
- Labels render as text, never as HTML, through `localize.string()`. A `#key` label shows the
  translated term (or the key itself if no term exists, as settings fields do), and any other
  label shows as-is.
- In run view (`runStatus` set), add buttons are hidden, as on other nodes.

Connection rules stay as they are: one line per source + handle, and the edge label becomes the
handle id, which becomes the saved `outcome`. That logic is already generic in `AutomationCanvas.tsx`
and needs no change for declared keys.

### Settings save → exits update

After a step's settings modal is submitted, if its action `hasDynamicOutcomes`, the canvas
calls `resolveOutcomes` with the new settings and re-renders that node's exits. Lines on keys
that still exist stay attached. Lines on keys that vanished move to **Missing outcome** exits
and are never deleted silently.

### UX spots

1. **First run:** a newly added outcome step shows all its exits, each with a "+" button, so
   the next action is obvious. No empty state needed.
2. **The mistake:** a removed or renamed option shows as a red **Missing outcome** exit on the
   canvas *before* publish, and publish names the step and outcome. If `resolveOutcomes` fails,
   the node keeps its last known exits and shows the standard error notification, so no lines
   get dropped.
3. **The wait:** while outcomes re-resolve after a settings save, the node keeps its previous
   exits. There's no spinner. The request is settings-only and fast, so a spinner would only
   flicker. Deliberate skip.
4. **The finish:** the exits visibly change to match the new settings. That's the confirmation.
   No toast.
5. **The return:** n/a. Exits come from saved settings, and the canvas already restores
   viewport and positions.
