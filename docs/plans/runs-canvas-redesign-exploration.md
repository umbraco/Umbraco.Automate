# Runs canvas redesign — exploration

Reference design (user-provided): clean horizontal flow, white cards with subtle shadow, dotted background, curved black edges. Each card surfaces **runtime data inline** (cadence/last/next on trigger, method+endpoint+duration on HTTP request, condition + true/false branch counts + duration on conditional) instead of just status colour.

## Current state

`run-canvas-view.element.ts` is a thin wrapper around the **editor** canvas (`ua-automation-canvas`):

- Sets `read-only`, disables drag/connect on every node, kills edge animation.
- Rebuilds nodes from `automation.trigger + steps + connections` using `modelToNodes` / `modelToEdges`.
- Overlays run state by setting `className="run-status-{status}"` and stuffing `runStatus` + `stepRun` onto `node.data`.
- All four node components (`TriggerNode`, `ActionNode`, `IfNode`, `SwitchNode`) hide the action-bar (edit/delete) when `runStatus` is set, but render exactly the same chrome as in the editor.

Status is communicated **only** via border colour in `canvas.styles.css`:
- completed → green border
- running → amber border
- failed → red border
- waitingforinput → amber border + glow
- pending → 50% opacity
- skipped → 40% opacity + dashed

There is no per-node runtime data surfaced today (no duration, no branch counts, no last/next-run for triggers).

## Reference image — what's actually different

| Aspect | Current | Reference |
| --- | --- | --- |
| Card chrome | Left coloured stripe + bold header w/ icon, type chips in body | No stripe; small icon + title + ⋯ menu; dividers between rows |
| Body content | Step alias chip + step ID chip | Multiple key-value rows with runtime data |
| Edges | xyflow default smooth-step, single colour, animated in editor | Curved bezier, **per-branch colour** (green = True, red = False) for conditionals |
| Background | xyflow `Background` (dots, default) | Same — dotted pattern, very light grey |
| Status | Border colour | Implicit (data values + small dot/badge). No status-coloured border in the reference |
| Trigger meta | none | "Cadence: Every 5 min", "Last run: …", "Next run: …" |
| Conditional meta | none | "Condition: order_total > 100", "True (Premium): 23", "False (Standard): 14", "Duration: 0.8s" |
| Action meta | none | First setting summary (e.g. "GET /v1/orders?since=…") + "Duration: 0.2s" |
| "Start" pill | none | Floating uppercase label above trigger card |

The visual changes are easy. **The data changes are the real cost.**

## Data gaps

What the current API gives us per run (`UaRunDetailModel.stepRuns`):

- `stepId`, `actionAlias`, `status`, `startedUtc`, `completedUtc`, `durationMs`, `retryCount`, `error`

What the reference image needs that we **don't** have:

1. **Trigger cadence / last / next** — these are *automation*-scoped, not *run*-scoped. Today the `automation.trigger.settings` blob has the cron expression but no resolved "Every 5 min" string and no last/next computed. The run view currently has no need to call into trigger scheduling at all. Doing this on the runs canvas means either:
   - Resolving cron → human description on the client (cheap, e.g. `cronstrue`).
   - Adding an API surface that returns last/next fire times for a scheduled trigger — and we'd want this in the automation workspace too, not just runs.
2. **Per-step inline summary** — for HTTP request, the reference shows "GET /v1/orders?since=…". We'd need each action to expose a "summary" projection of its settings. Settings are a freeform POCO blob today; rendering "first useful line" requires either:
   - A per-action-type renderer (needs an extension point on the catalogue).
   - A generic "first non-secret setting" formatter — risks showing junk.
3. **Conditional branch counts** — "True: 23 / False: 14". This implies iteration counting per branch. `stepRuns` is keyed by `stepId`, but the recent commit `858217c refactor(core): Scope step outputs by iteration path` shows iteration paths now exist. We'd need either:
   - Aggregate counts on `StepRun` (cheap, server-side).
   - The client to count step runs whose iteration path passes through each branch handle (needs the iteration path to be in `UaStepRunModel`, which it isn't today).

`durationMs` is already there — that's the easiest reference-design element to land.

## Implementation options

### A. Cosmetic-only redesign (low effort)
Change just the visual treatment: remove stripe, restyle header, swap edge style to bezier, colour true/false edges, drop status-as-border in favour of small status dot, surface `durationMs` on every node. **Don't** chase cadence / last / next / branch counts.

- Touches: `canvas.styles.css`, optionally a new run-only node component set, edge component for branch colouring.
- Won't visually match the reference exactly — the trigger card body will be near-empty.
- Pros: 1–2 day spike, no API or backend changes.
- Cons: Doesn't really deliver the "rich runtime card" promise of the reference.

### B. Run-only nodes + duration + branch counts (medium effort)
Fork `TriggerNode` / `ActionNode` / `IfNode` / `SwitchNode` into `Run*Node` variants registered only by `run-canvas-view`. Surface duration everywhere; for `IfNode`/`SwitchNode` count `stepRuns` per outgoing branch using the iteration-path data added in `858217c`. Keep editor canvas untouched.

- Touches: 4 new tsx components, `run-canvas-view.element.ts` (add a `nodeTypes` prop or branch on `read-only`), `UaStepRunModel` (expose iteration path), `canvas.styles.css` (run-specific tokens).
- Pros: Clean separation, no risk to the editor, hits most of the reference image.
- Cons: Some duplication; needs a small backend change for iteration path on the read model.

### C. Full reference parity (high effort)
B + cron-description + per-action settings summary extension point + a "trigger schedule" API for last/next.

- Touches: catalogue manifest extensions, server endpoint for trigger schedule, automation.trigger settings projection.
- Pros: Matches the design and creates reusable surfaces (settings summary will be useful in collection views too).
- Cons: Multi-week effort; cron-next is also a new responsibility for the scheduler.

## Recommendation

Start with **B**. It hits the visible difference that matters most — runtime data rendered into the card — without committing us to a backend feature (per-action summary projection) that's better designed alongside other consumers. The edge-colouring and bezier change can ride along in the same PR since they're scoped to the run canvas only.

If we ever need C, the run-only node components from B are already the right place to drop in a richer summary block; we just expand what's read off `stepRun` / `automation.trigger.settings`.

## Files to look at

- `Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/src/run/workspace/run/views/run-canvas-view.element.ts` — current entry point.
- `Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/src/automation/workspace/automation/canvas/nodes/*.tsx` — node components to fork.
- `Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/src/automation/workspace/automation/canvas/canvas.styles.css` — shared styles, run-specific class hooks already exist.
- `Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/src/automation/workspace/automation/canvas/edges/AutomationEdge.tsx` — edge to fork for branch colouring.
- `Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/src/run/types.ts` — `UaStepRunModel` is where iteration path would land for branch counts.
