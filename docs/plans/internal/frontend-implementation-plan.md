# Umbraco.Automate Backoffice Frontend — Implementation Plan

## Context

The backend Management API is built and functional. The frontend scaffolding (Vite, TypeScript, package.json, empty `app.ts`/`manifests.ts`) exists but no UI components have been implemented. This plan builds the complete backoffice experience in phases, each producing a testable milestone. It follows the Umbraco.AI frontend patterns as a reference implementation.

**Base path:** `Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/`

### Conventions

| Convention | Value |
|---|---|
| Element prefix | `ua-` |
| Manifest alias prefix | `UmbracoAutomate.` |
| Section alias | `Ua.Section.Automate` |
| Section pathname | `automate` |
| Entity types | `ua:automation`, `ua:automation-root`, `ua:run` |

### Existing API Endpoints

| Area | Endpoints |
|---|---|
| Automations | GET (paged), GET by id, POST, PUT, DELETE, publish, unpublish, trigger |
| Catalogue | GET all actions, GET all triggers (with SettingsSchema) |
| Runs | GET by automation, GET by id |

### Not Available Yet (defer)

- No folder/group API endpoints (GroupId exists on domain model but not in API)
- No global settings endpoints
- No HITL approval endpoints

---

## Phase 0: Scaffolding + OpenAPI Client ✅

**Status:** Complete

**Goal:** Build compiles, "Automations" section appears in backoffice with a placeholder dashboard, API client is generated.

### Steps

1. ✅ **Add dev dependencies** to `package.json`: `@hey-api/openapi-ts`, `node-fetch`, `chalk`
2. ✅ **Create `scripts/generate-openapi.js`** — copy from Umbraco.AI, point at `automate-management` swagger endpoint
3. ✅ **Add `generate-client` npm script** — `node scripts/generate-openapi.js https://localhost:44331/umbraco/swagger/automate-management/swagger.json`
4. ✅ **Generate API client** into `src/api/` (run against running demo site)
5. ✅ **Wire up `app.ts`** — consume `UMB_AUTH_CONTEXT`, configure API client auth (copy AI pattern from `D:\Umbraco.AI\...\Client\src\app.ts`)
6. ✅ **Create section + sidebar + placeholder dashboard**

### Files

```
src/
├── api/                          # Generated
├── app.ts                        # Auth init
├── manifests.ts                  # Aggregate all feature manifests
├── index.ts                      # Internal barrel
├── vite-env.d.ts                 # Vite client types (for ?inline CSS etc.)
├── lang/
│   ├── en.ts                     # Localization keys
│   └── manifests.ts
├── core/
│   ├── index.ts
│   ├── manifests.ts
│   ├── menu/
│   │   └── types.ts              # UaEntityContainerMenuItemManifest (legacy, unused)
│   ├── events/
│   │   ├── entity-action.event.ts
│   │   └── index.ts
│   ├── entity-action/
│   │   └── delete/               # UaDeleteActionBase
│   ├── entity-bulk-action/
│   │   └── delete/               # UaBulkDeleteActionBase
│   └── utils/
│       ├── datetime.utils.ts
│       ├── event.utils.ts
│       └── index.ts
├── section/
│   ├── constants.ts              # UA_SECTION_ALIAS, UA_MENU_ALIAS
│   ├── manifests.ts              # Section + sidebar + menu
│   ├── menu/
│   │   └── manifests.ts
│   ├── sidebar/
│   │   └── manifests.ts          # menuWithEntityActions kind, entityType on root
│   └── dashboard/
│       ├── manifests.ts
│       └── automate-dashboard.element.ts  # Placeholder
scripts/
└── generate-openapi.js
```

---

## Phase 1: Automation Collection (List View) ✅

**Status:** Complete

**Goal:** Sidebar shows "Automations" menu item. Clicking it shows a table listing automations from the API with status badges and basic actions.

### Steps

1. ✅ **Define entity types** and constants
2. ✅ **Build repository layer**: detail + collection data sources, repositories, store
3. ✅ **Build type mapper** converting API DTOs to frontend models
4. ✅ **Create collection view** with table showing Name, Status badge, IsEnabled, DateModified
5. ✅ **Register menu item** in sidebar
6. ✅ **Add entity actions**: Create (on root), Delete, Bulk Delete

### Files

```
src/automation/
├── constants.ts                  # Re-exports entity, workspace, repository, collection, tree constants
├── entity.ts                     # UA_AUTOMATION_ENTITY_TYPE, UA_AUTOMATION_ROOT_ENTITY_TYPE
├── types.ts                      # UaAutomationDetailModel, UaAutomationItemModel
├── type-mapper.ts
├── manifests.ts
├── menu/
│   └── manifests.ts              # kind: "tree" (changed from entityContainer)
├── repository/
│   ├── constants.ts
│   ├── manifests.ts
│   ├── detail/
│   │   ├── automation-detail.server.data-source.ts
│   │   ├── automation-detail.repository.ts
│   │   └── automation-detail.store.ts
│   └── collection/
│       ├── automation-collection.server.data-source.ts
│       └── automation-collection.repository.ts
├── collection/
│   ├── constants.ts                  # UA_AUTOMATION_COLLECTION_ALIAS
│   ├── manifests.ts
│   ├── action/
│   │   ├── manifests.ts
│   │   └── automation-create-collection-action.element.ts
│   ├── bulk-action/
│   │   ├── manifests.ts
│   │   └── automation-bulk-delete.action.ts
│   └── views/table/
│       └── automation-table-collection-view.element.ts
├── entity-actions/
│   ├── manifests.ts
│   ├── automation-create.action.ts   # Navigates to create workspace
│   └── automation-delete.action.ts   # Extends UaDeleteActionBase
└── workspace/
    ├── constants.ts
    ├── manifests.ts
    └── automation-root/
        ├── manifests.ts          # Default workspace + collection view
        └── paths.ts
```

---

## Phase 2: Automation Workspace (Edit, No Canvas) ✅

**Status:** Complete

**Goal:** Create/edit automations with name, alias, save/publish/unpublish. Two tabs: placeholder Workflow tab and Info tab with metadata.

### Steps

1. ✅ **Create routable workspace context** extending `UmbSubmittableWorkspaceContextBase`
   - Routes: `create` and `edit/:unique`
   - Methods: `scaffold()`, `load()`, `submit()` (save), `publish()`, `unpublish()`
   - Dispatches `UmbRequestReloadStructureForEntityEvent` on save/publish/unpublish
   - Dispatches `UmbRequestReloadChildrenOfEntityEvent` on create (new item in tree)
2. ✅ **Create workspace editor element** with name/alias header
3. ✅ **Create two workspace views**:
   - Design (was "Workflow" — renamed)
   - Info (version, dates, status, publish info)
4. ✅ **Create workspace actions**: Save, Save and Publish, Unpublish
5. ✅ **Wire up collection create action** to navigate to workspace create route

### Changes from original plan

- Workspace view label changed from "Workflow" to **"Design"**
- Save and Publish is a separate button (not a split button) — uses custom action class that calls `submit()` then `publish()`
- Unpublish has `UMB_WORKSPACE_ENTITY_IS_NEW_CONDITION_ALIAS` condition so it's hidden for new automations
- Workspace context dispatches tree reload events via `UMB_ACTION_EVENT_CONTEXT` after save/create/publish/unpublish

### Files

```
src/automation/workspace/automation/
├── manifests.ts
├── paths.ts
├── automation-workspace.context.ts
├── automation-workspace.context-token.ts
├── automation-workspace-editor.element.ts
├── actions/
│   ├── automation-save-and-publish.action.ts
│   └── automation-unpublish.action.ts
└── views/
    ├── automation-workflow-workspace-view.element.ts   # "Design" tab (canvas integration)
    └── automation-info-workspace-view.element.ts
```

---

## Phase 3: Catalogue + Node Picker Modal ✅

**Status:** Complete

**Goal:** Modal that fetches triggers/actions from catalogue API, grouped by category with search, returns selected item.

### Steps

1. ✅ **Create catalogue repository** — fetches and caches triggers and actions lists
2. ✅ **Create node picker modal** with `UmbModalToken`
   - Takes `mode: 'trigger' | 'action'`
   - Groups items by `Group` property
   - Shows icon, name, description via `uui-ref-node`
   - Client-side search filtering
   - Returns selected item on `@open` event (not `selectable` — that's for checkbox selection)

### Changes from original plan

- Uses `@open` event on `uui-ref-node` instead of `selectable` + `@selected` (CMS pattern)
- Modal type is `sidebar` with size `small`

### Backend fixes required during this phase

- **Catalogue returning empty arrays**: `LazyCollectionBuilderBase` needed explicit `TypeLoader.GetTypesWithAttribute` producer registration
- **`System.NotSupportedException` serializing `System.Type`**: Created serializable response DTOs (`EditableModelSchemaResponseModel`, `TriggerOutputPropertyResponseModel`) replacing domain objects that had `Type` and `ValidationAttribute` properties
- **`CatalogueMapDefinition`**: Updated to map domain types to serializable DTOs

### Files

```
src/catalogue/
├── constants.ts
├── types.ts                      # UaCatalogueItemModel, UaTriggerCatalogueItemModel, UaActionCatalogueItemModel
├── type-mapper.ts                # toActionModel(), toTriggerModel()
├── manifests.ts
├── repository/
│   ├── catalogue.repository.ts   # UmbRepositoryBase with in-memory caching
│   └── catalogue.server.data-source.ts
└── modals/
    ├── manifests.ts
    └── node-picker/
        ├── node-picker-modal.element.ts
        ├── node-picker-modal.token.ts
        └── types.ts
```

---

## Phase 4: React Flow Canvas ✅

**Status:** Complete

**Goal:** Design tab renders an interactive node-based canvas. Add/connect/drag nodes. Graph persists to API.

### Steps

1. ✅ **Add dependencies**: `react`, `react-dom`, `@xyflow/react`, `@vitejs/plugin-react`
2. ✅ **Update build config**:
   - `vite.config.ts`: add React plugin, `define: { "process.env.NODE_ENV": JSON.stringify("production") }` to avoid runtime `process` reference and enable tree-shaking
   - `tsconfig.json`: add `"jsx": "react-jsx"` for `.tsx` support
3. ✅ **Create Lit-React bridge element** (`<ua-automation-canvas>`):
   - Creates React root in `firstUpdated()`, unmounts in `disconnectedCallback()`
   - Receives `nodes`, `edges`, `viewport` as Lit properties
   - Injects React Flow CSS + custom CSS into shadow DOM via `?inline` imports (not `<head>` injection)
   - Emits custom DOM events: `ua:canvas-change`, `ua:add-node-request`
4. ✅ **Create React components**:
   - `AutomationCanvas.tsx` — main ReactFlow wrapper with Background, Controls, MiniMap
   - `TriggerNode.tsx` — styled trigger node (indigo header, source handle at bottom)
   - `ActionNode.tsx` — action step node with settings pencil button, target+source handles
   - `AutomationEdge.tsx` — smooth step edge with optional label
5. ✅ **Create model converters**:
   - `model-to-flow.ts` — domain models -> React Flow nodes/edges. Trigger uses special `__trigger__` ID. `Guid.Empty` in connections maps to trigger.
   - `flow-to-model.ts` — React Flow -> domain models. Trigger connections use `Guid.Empty` (`00000000-0000-0000-0000-000000000000`) for `sourceStepId` (required by C# `System.Guid`).
6. ✅ **Replace workflow view placeholder** with canvas integration:
   - Canvas fills full workspace
   - Toolbar with "Add Trigger" / "Add Action" buttons opens node picker modal
   - Double-click on pane also opens node picker
   - External prop changes sync into React state via `useEffect` (not just initial state)

### Changes from original plan

- **Shadow DOM CSS injection**: React Flow CSS and custom styles are imported as `?inline` strings and injected via `<style>` element into the shadow root, since `<head>` injection doesn't reach shadow DOM. Added `src/vite-env.d.ts` with `/// <reference types="vite/client" />` for `?inline` type support.
- **Controlled props**: `AutomationCanvas` accepts live `nodes`/`edges`/`viewport` props (not `initialNodes`/`initialEdges`) with `useEffect` sync, so external changes (e.g. adding a node from the modal) update React Flow immediately.
- **Guid.Empty for trigger connections**: `flowToConnections` uses `"00000000-0000-0000-0000-000000000000"` when source is trigger node (empty string `""` fails C# `System.Guid` deserialization).
- **`process.env.NODE_ENV` define**: Prevents `ReferenceError: process is not defined` at runtime and enables production tree-shaking (bundle ~572KB vs ~1224KB).
- Events renamed: `ua:nodes-change`/`ua:edges-change` simplified to single `ua:canvas-change` event with combined detail.

### Backend fixes required during this phase

- **`System.InvalidOperationException` scope disposal**: `EFCoreOutboxStore.ClaimNextAsync` was calling wrong `ExecuteWithContextAsync` overload — `OutboxMessage?` was treated as DbContext type parameter. Fixed by removing explicit type parameter and using type inference.

### Files

```
src/automation/workspace/automation/canvas/
├── ua-automation-canvas.element.ts       # Lit bridge (shadow DOM CSS injection)
├── AutomationCanvas.tsx                  # React Flow wrapper (controlled props)
├── canvas.styles.css                     # Node, edge, handle styling
├── types.ts                              # TriggerNodeData, ActionNodeData, CanvasState, etc.
├── nodes/
│   ├── TriggerNode.tsx
│   ├── ActionNode.tsx
│   └── node-types.ts
├── edges/
│   └── AutomationEdge.tsx
└── utils/
    ├── model-to-flow.ts                  # Domain -> React Flow (Guid.Empty -> __trigger__)
    └── flow-to-model.ts                  # React Flow -> Domain (__trigger__ -> Guid.Empty)
```

### Key architectural decisions

- **React is bundled** into the workflow view chunk (~590KB), not shared with CMS host
- **Lit owns state, React renders** — one-directional data flow via properties down, DOM events up
- **React never consumes Umbraco contexts** — all data passed through the Lit bridge
- **Canvas state** (viewport, zoom, trigger position) serialized as JSON string in `Automation.CanvasState`
- **CSS injected into shadow DOM** — `?inline` Vite imports, not global `<head>` injection

---

## Phase 4.5: Sidebar Tree ✅

**Status:** Complete (added post-plan)

**Goal:** Sidebar shows a navigable tree of automations (like CMS Content tree), replacing the static `entityContainer` menu item. Ready for folders/hierarchy.

This phase was **not in the original plan** — it was added because the sidebar needed to show individual automation items for navigation, with a Create action on the root.

### Steps

1. ✅ **Create tree types** extending `UmbTreeItemModel` with `status` and `isEnabled`
2. ✅ **Create tree server data source** calling `AutomationsService.getAutomations()` for root items, empty for children/ancestors (folders will add hierarchy later)
3. ✅ **Create tree repository** extending `UmbTreeRepositoryBase` with `requestTreeRoot()`
4. ✅ **Create tree store** extending `UmbUniqueTreeStore`
5. ✅ **Create tree context and element** extending defaults (minimal overrides)
6. ✅ **Create tree item context and element** extending defaults with name/icon observation
7. ✅ **Change menu item** from `kind: "entityContainer"` to `kind: "tree"` with `treeAlias` and `hideTreeRoot: true`
8. ✅ **Update sidebar manifest** to include `entityType: UA_AUTOMATION_ROOT_ENTITY_TYPE` (enables entity actions on root `...` menu)

### Files

```
src/automation/tree/
├── constants.ts                  # UA_AUTOMATION_TREE_ALIAS, repository alias, store alias
├── types.ts                      # UaAutomationTreeItemModel, UaAutomationTreeRootModel
├── automation-tree.server.data-source.ts   # Calls getAutomations(), getChildrenOf → empty
├── automation-tree.repository.ts           # Extends UmbTreeRepositoryBase
├── automation-tree.store.ts                # Extends UmbUniqueTreeStore
├── automation-tree.context.ts              # Extends UmbDefaultTreeContext
├── automation-tree.element.ts              # Extends UmbDefaultTreeElement
├── manifests.ts
└── tree-item/
    ├── automation-tree-item.context.ts     # Extends UmbDefaultTreeItemContext
    └── automation-tree-item.element.ts     # Extends UmbTreeItemElementBase
```

---

## Phase 5: Node Settings Modal ✅

**Status:** Complete

**Goal:** Clicking pencil icon on a node opens a form auto-generated from `EditableModelSchema`.

### Steps

1. ✅ **Create shared settings form component** (`core/components/settings-form/`)
   - Renders `EditableModelFieldDescriptorResponseModel[]` as form fields using `umb-property-layout` with `orientation="vertical"`
   - Maps `EditorUiAlias` to editor types via `field-mapper.ts`; infers from `propertyType` when no alias specified
   - Groups fields by `Group` in `<uui-box>` containers, orders by `SortOrder`
   - Marks required fields with `mandatory` attribute
   - Styled to match `umb-property-type-workspace-view-settings` CMS pattern
2. ✅ **Create node settings modal** (for action steps)
   - Receives: stepId, actionAlias, currentSettings, schema
   - Renders settings form in sidebar modal
   - Returns updated settings dictionary
3. ✅ **Create trigger settings modal** (for trigger node)
   - Same pattern but for trigger configuration
   - Added pencil settings button to `TriggerNode.tsx` (was missing)
4. ✅ **Wire up canvas events** — `ua:node-settings-open` handled in workflow workspace view
   - Looks up schema from catalogue repository (cached)
   - Opens appropriate modal based on `nodeType` ("trigger" | "action")
   - On submit, updates settings in workspace context model

### Changes from original plan

- **TriggerNode settings button**: Added pencil (✏️) button and `ua:node-settings-open` event dispatch to `TriggerNode.tsx`, matching `ActionNode.tsx` pattern
- **Styling**: Modals and settings form follow `umb-property-type-workspace-view-settings` patterns — vertical `umb-property-layout`, `<uui-box>` grouping, description via `<small slot="description">`, full-width inputs
- **Catalogue lookup**: Workflow view creates a `UaCatalogueRepository` to look up schemas by alias when opening settings modals (cached after first request)

### Files

```
src/core/components/
└── settings-form/
    ├── settings-form.element.ts   # <ua-settings-form> using umb-property-layout vertical
    └── field-mapper.ts            # EditorUiAlias + propertyType -> FieldEditorType

src/automation/modals/
├── manifests.ts                   # Modal manifest registrations
├── node-settings/
│   ├── node-settings-modal.element.ts
│   ├── node-settings-modal.token.ts
│   └── types.ts
└── trigger-settings/
    ├── trigger-settings-modal.element.ts
    ├── trigger-settings-modal.token.ts
    └── types.ts
```

### Reference files
- `Umbraco.Automate/src/Umbraco.Automate.Core/Settings/EditableModelSchema.cs` — schema structure driving the form
- `Umbraco-CMS/.../property-workspace-view-settings.element.ts` — styling reference

### Verification
- Click pencil on trigger node -> trigger settings modal opens with correct fields
- Click pencil on action node -> action settings modal opens with correct fields
- Change values, save -> settings update on workspace model
- Save automation -> settings persist to API
- Reopen -> settings restored correctly

---

## Phase 6: Run Explorer

**Status:** Complete

**Goal:** View automation run history with step-by-step status visualisation on a read-only canvas. Runs are a **dashboard** on the section (not a sidebar tree item).

### Steps

1. ✅ **Create run feature module** with repository, data source, type mapper
2. ✅ **Register "Runs" as a section dashboard** — `type: "dashboard"` conditioned on `Ua.Section.Automate`, with pathname `runs`
3. ✅ **Create run dashboard element** — table with status, automation name, started, duration, initiator (fetches from all automations, not Umbraco collection infrastructure)
4. ✅ **Create run detail workspace** — read-only canvas showing nodes colour-coded by step status, loads automation for canvas structure
5. ✅ **Add step data inspection** — Details workspace view shows step runs with expandable error/timing info
6. ✅ **Add "Runs" tab** to automation workspace (filtered by automation ID, hidden for new automations)

### Files

```
src/run/
├── constants.ts
├── entity.ts
├── types.ts
├── type-mapper.ts
├── manifests.ts
├── repository/
│   ├── constants.ts
│   ├── manifests.ts
│   ├── detail/
│   │   ├── run-detail.server.data-source.ts
│   │   └── run-detail.repository.ts
│   └── collection/
│       ├── run-collection.server.data-source.ts
│       └── run-collection.repository.ts
├── dashboard/
│   ├── manifests.ts                              # Dashboard manifest (kind: "default")
│   └── run-dashboard.element.ts                  # Table with status, name, started, duration
└── workspace/
    ├── constants.ts
    ├── manifests.ts
    └── run/
        ├── manifests.ts
        ├── paths.ts
        ├── run-workspace.context.ts
        ├── run-workspace.context-token.ts
        ├── run-workspace-editor.element.ts   # Header with status tag
        └── views/
            ├── run-canvas-view.element.ts    # Read-only canvas with status overlay
            └── run-details-view.element.ts   # Step data inspection

src/automation/workspace/automation/views/
└── automation-runs-workspace-view.element.ts  # "Runs" tab on automation workspace
```

### Verification
- "Runs" dashboard tab appears on the section (alongside Welcome/Dashboard)
- Dashboard shows global run list as a table
- Click a run -> opens read-only canvas with nodes coloured by status
- Click a node -> shows input/output JSON, error, duration
- Automation workspace "Runs" tab -> shows filtered runs for that automation

---

## Phase 7: Dashboard

**Status:** Complete

**Goal:** Replace placeholder dashboard with real status cards and activity feed. This is a **dashboard** on the section (same pattern as Phase 6 Runs dashboard).

### Steps

1. ✅ **Update existing section dashboard** — kept as `type: "dashboard"` conditioned on `Ua.Section.Automate`, with pathname `welcome`
2. ✅ **Build dashboard with status cards**: Published, Draft, Inactive, Failed Runs, In Progress counts
3. ✅ **Build activity list**: Recent runs with status badge, automation name, timestamp, links to run workspace
4. ✅ **Wire up links**: activity items link to run workspace detail view

### Files

```
src/section/dashboard/
├── manifests.ts                      # Dashboard manifest (kind: "default", already exists)
├── automate-dashboard.element.ts     # Replace placeholder
└── components/
    ├── status-cards.element.ts
    └── activity-list.element.ts
```

### Verification
- Section root shows dashboard with real data (default tab when entering section)
- "Runs" tab also appears as a sibling dashboard on the section
- Status cards show correct counts
- Links navigate to correct filtered views or Runs dashboard
- Activity list shows recent runs

---

## Phase Dependency Graph

```
Phase 0 (Scaffold) ✅
├── Phase 1 (Collection) ✅
│   └── Phase 2 (Workspace) ✅
│       └── Phase 4 (Canvas) ✅ ← requires Phase 3
│           ├── Phase 4.5 (Sidebar Tree) ✅ ← added post-plan
│           ├── Phase 5 (Settings Modal) ✅
│           └── Phase 6 (Run Explorer) ✅
│               └── Phase 7 (Dashboard) ✅
└── Phase 3 (Catalogue + Picker) ✅ ← parallel with Phase 1-2
```

All phases are complete.

---

## Deferred (not in scope)

- ~~**Folder/tree organization** — no backend API for groups/folders yet~~ → Tree infrastructure is in place (Phase 4.5), ready for hierarchy when folder API is added. `getChildrenOf`/`getAncestorsOf` return empty results for now.
- **HITL approvals** — Phase 2 backend feature
- **Version diff viewer** — Phase 3+ per spec
- **Expression editor** — for input mapping expressions
- **AI configuration panel** — depends on AI integration
- **Global settings panel** — no backend endpoints yet
- **Drag-drop reordering** — requires folder API

---

## Known Issues / Tech Debt

- Canvas node styling needs refinement (current styles are functional but basic)
- `core/menu/types.ts` (`UaEntityContainerMenuItemManifest`) is now unused after tree migration — can be removed
- React Flow bundle is ~590KB — could be reduced with lazy loading if needed
- OpenAPI client should be regenerated after catalogue API DTO changes (`EditableModelSchemaResponseModel`)
