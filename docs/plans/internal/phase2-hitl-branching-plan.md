# Phase 2: HITL Approval UI & If/Switch Canvas Nodes

## Context

Phase 2 of the engineering spec introduces HITL (Human-in-the-Loop) approvals and conditional branching (If/Switch). The **backend for both features is already complete** — `RequestApprovalAction`, `ApprovalDecision`, approval API endpoints, `IfControlFlow`, `SwitchControlFlow`, `ConditionSet`/`ConditionEvaluator`, and `WorkflowCompiler` outcome wiring all exist and work. This plan covers the **frontend implementation** needed to expose these features in the backoffice.

Working directory: `D:\DXP\Umbraco.Automate\.worktrees\phase2-exploration`
Frontend base: `Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/src/`

---

## Sub-Phase 1: HITL Approval Dashboard & Decision Modal

**Goal:** Pending approvals visible and actionable from the Automate section.

### 1A. Approval Dashboard Tab

**New files:**
- `approval/dashboard/approval-dashboard.element.ts` — Table of pending approvals (follow `run/dashboard/run-dashboard.element.ts` pattern)
  - Calls `ApprovalsService.getApprovalsPending()` (already in generated client)
  - Columns: Automation Name, Prompt, Requested At, Actions (Approve/Reject buttons)
  - Buttons open approval decision modal
- `approval/dashboard/manifests.ts` — Dashboard manifest (`Ua.Dashboard.Approvals`, pathname `approvals`, weight 4)
- `approval/manifests.ts` — Aggregates approval manifests

**Modified:**
- `manifests.ts` (root) — Import and spread `approvalManifests`
- `lang/en.ts` — Add `uaApproval` section keys

### 1B. Approval Decision Modal

**New files:**
- `approval/modals/approval-decision/approval-decision-modal.element.ts` — Modal with prompt display, comment textarea, Approve/Reject buttons
  - Calls `ApprovalsService.postApprovalsByRunIdStepsByStepIdDecision()`
  - Follow `automation/modals/node-settings/node-settings-modal.element.ts` pattern
- `approval/modals/approval-decision/approval-decision-modal.token.ts` — Modal token
- `approval/modals/approval-decision/types.ts` — `UaApprovalDecisionModalData` (runId, stepId, automationName, prompt) and `UaApprovalDecisionModalValue` (outcome)

### 1C. Run Canvas Status Styling

**Modified:**
- `automation/workspace/automation/canvas/canvas.styles.css` — Add run-status classes:
  - `.run-status-completed` → green border
  - `.run-status-running` → amber border
  - `.run-status-failed` → red border
  - `.run-status-waitingforinput` → amber border + glow
  - `.run-status-pending` → dimmed opacity

---

## Sub-Phase 2: If/Switch Canvas Node Components

**Goal:** New React Flow node types with distinct styling and multiple output handles.

### 2A. Node Components

**New files:**
- `automation/workspace/automation/canvas/nodes/IfNode.tsx` — Amber-themed node with:
  - Target handle at top
  - Two source handles at bottom: `id="true"` (left 30%) and `id="false"` (right 70%)
  - Small handle labels ("True" / "False")
  - Settings button dispatches `ua:node-settings-open` with `nodeType: "action"` (reuses existing modal infra)
- `automation/workspace/automation/canvas/nodes/SwitchNode.tsx` — Purple-themed node with:
  - Target handle at top
  - Dynamic source handles from `data.cases` array + always a `"default"` handle
  - Handles evenly distributed along bottom edge
  - Small handle labels per case name

### 2B. Registration & Styling

**Modified:**
- `canvas/nodes/node-types.ts` — Add `if: IfNode`, `switch: SwitchNode`
- `canvas/canvas.styles.css` — Add `.ua-node--if` (amber #f59e0b), `.ua-node--switch` (purple #8b5cf6), handle label styles
- `canvas/types.ts` — Add optional `cases?: string[]` to `ActionNodeData` for switch nodes
- `canvas/AutomationCanvas.tsx` — Update MiniMap `nodeColor` for if/switch types

---

## Sub-Phase 3: Model Converters & Node Picker Integration

**Goal:** If/Switch nodes created, saved, and loaded correctly; picker shows control flow items.

### 3A. Model Converters

**Modified:**
- `canvas/utils/model-to-flow.ts` (`modelToNodes`) — Detect `actionAlias` = `umbracoAutomate.if` → node type `"if"`; `umbracoAutomate.switch` → node type `"switch"` with `cases` extracted from `settings.Cases`
- No changes needed to `flow-to-model.ts` — existing mapping already handles `sourceHandle` and `label`/`outcome` correctly

### 3B. Catalogue & Node Picker

**Modified:**
- `catalogue/repository/catalogue.repository.ts` — Add `requestControlFlows()` with cache
- `catalogue/repository/catalogue.server.data-source.ts` — Add `getControlFlows()` calling `CatalogueService.getCatalogueControlFlows()`
- `catalogue/type-mapper.ts` — Add `toControlFlowModel()`
- `catalogue/types.ts` — Add `UaControlFlowCatalogueItemModel`
- `catalogue/modals/node-picker/node-picker-modal.element.ts` — When mode is `"action"`, also fetch and merge control flows (they have `group: "Control Flow"` from backend)
- `automation/workspace/automation/views/automation-workflow-workspace-view.element.ts`:
  - Update `#buildCatalogueNames()` to include control flows
  - Update settings modal lookup to also check control flow catalogue items

### 3C. Canvas Connection Validation

**Modified:**
- `canvas/AutomationCanvas.tsx`:
  - `onConnect`: Key edge replacement on `source + sourceHandle` (not just `source`), so each If/Switch handle can have its own connection
  - Auto-set edge `label` from `sourceHandle` when connecting from If/Switch handles
  - `isValidConnection`: Adjust remaining-edges filter to match on source+sourceHandle

---

## Sub-Phase 4: Condition Builder & Switch Case Builder Property Editors

**Goal:** Custom editors for configuring conditions in the settings modal.

### 4A. Condition Builder

**New files:**
- `core/components/condition-builder/condition-builder.element.ts` — Implements `UmbPropertyEditorUiElement`
  - Renders `ConditionSet` as DNF: groups (OR) containing conditions (AND)
  - Each condition: left operand input, operator dropdown (`ConditionOperator` values), right operand input (hidden for IsEmpty/IsNotEmpty)
  - Add/remove condition, add/remove group buttons
  - Visual "AND"/"OR" separators
- `core/components/condition-builder/manifests.ts` — Register as `UmbracoAutomate.PropertyEditorUi.ConditionBuilder`

### 4B. Switch Case Builder

**New files:**
- `core/components/switch-case-builder/switch-case-builder.element.ts` — Implements `UmbPropertyEditorUiElement`
  - Renders list of `SwitchCase` items, each with name input + nested `<ua-condition-builder>`
  - Add/remove case, reorder buttons
  - Case name becomes the outcome handle label on the canvas
- `core/components/switch-case-builder/manifests.ts` — Register as `UmbracoAutomate.PropertyEditorUi.SwitchCaseBuilder`

**Modified:**
- `core/manifests.ts` — Import condition-builder and switch-case-builder manifests
- `lang/en.ts` — Add condition builder and switch case builder localization keys

---

## Dependency Graph

```
Sub-Phase 1 (Approval UI)    Sub-Phase 2 (Node Components)
  [independent]                         |
                                        v
                              Sub-Phase 3 (Converters + Picker)
                                        |
                                        v
                              Sub-Phase 4 (Property Editors)
```

Sub-Phases 1 and 2 are independent and can be built in parallel.

---

## Key Existing Files

| File | Role |
|------|------|
| `run/dashboard/run-dashboard.element.ts` | Dashboard pattern reference |
| `automation/modals/node-settings/node-settings-modal.element.ts` | Modal pattern reference |
| `canvas/nodes/ActionNode.tsx` | Node component pattern reference |
| `canvas/utils/model-to-flow.ts` | Model → React Flow conversion |
| `canvas/AutomationCanvas.tsx` | Canvas wrapper, connection handling |
| `catalogue/repository/catalogue.repository.ts` | Catalogue with caching |
| `core/components/settings-form/settings-form.element.ts` | Uses `umb-property` with `property-editor-ui-alias` from field descriptor |
| `api/sdk.gen.ts` | Already has `ApprovalsService` and `CatalogueService.getCatalogueControlFlows` |

## Verification

1. **Approval flow:** Create automation with Request Approval action → trigger it → verify "Approvals" tab shows pending item → approve via modal → verify run completes
2. **If node:** Add If step from picker → configure conditions in settings modal → connect true/false handles to different actions → save → reload → verify model persists correctly
3. **Switch node:** Add Switch step → configure 3 cases → connect each handle → save → reload → verify dynamic handles and connections preserved
4. **Run explorer:** Run an automation with If/Switch steps → verify run canvas shows correct branch outcome highlighting
5. **Build:** `npm run build` succeeds with no TypeScript errors
