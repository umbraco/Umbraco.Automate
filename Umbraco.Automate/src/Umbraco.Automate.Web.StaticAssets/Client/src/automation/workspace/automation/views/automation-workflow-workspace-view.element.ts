import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_MODAL_MANAGER_CONTEXT, UMB_CONFIRM_MODAL } from "@umbraco-cms/backoffice/modal";
import type { Node, Edge, Viewport } from "@xyflow/react";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import type { UaAutomationDetailModel } from "../../../types.js";
import {
    modelToNodes,
    modelToEdges,
    getContinuationSourceHandle,
    getOutcomeDeclaringNodeIds,
    isActionNodeType,
    ANY_RESULT_HANDLE,
    computeReachableFromTrigger,
    TRIGGER_NODE_ID,
    BODY_HANDLE,
    PARALLEL_ALIAS,
} from "../canvas/utils/model-to-flow.js";
import {
    DEFAULT_NODE_HEIGHT,
    DEFAULT_NODE_WIDTH,
    NEW_NODE_GAP,
    findFreePosition,
} from "../canvas/utils/placement.js";
import { flowToSteps, flowToConnections, flowToCanvasState, flowToTrigger } from "../canvas/utils/flow-to-model.js";
import type { CanvasState, CanvasChangeDetail, CatalogueLookupEntry, AddNodeRequestDetail, NodeSettingsOpenDetail, NodeDeleteRequestDetail, EdgeFilterOpenDetail } from "../canvas/types.js";
import { UA_NODE_PICKER_MODAL } from "../../../../catalogue/modals/node-picker/node-picker-modal.token.js";
import { UA_NODE_SETTINGS_MODAL } from "../../../modals/node-settings/node-settings-modal.token.js";
import { UA_TRIGGER_SETTINGS_MODAL } from "../../../modals/trigger-settings/trigger-settings-modal.token.js";
import { UA_EDGE_FILTER_MODAL } from "../../../modals/edge-filter/edge-filter-modal.token.js";
import type { UaStepOutcome } from "../../../../catalogue/types.js";
import { UaCatalogueRepository } from "../../../../catalogue/repository/catalogue.repository.js";
import type {
    EditableModelSchemaModel,
    StepConfigurationModel,
    StepConnectionModel,
} from "../../../../api/types.gen.js";
import { UA_EMPTY_GUID } from "../../../../core/index.js";
import "../canvas/ua-automation-canvas.element.js";

/**
 * Room reserved for a step spliced into an existing connection. Node heights are content-driven
 * and not known until React Flow measures the new node, so this is a generous estimate that covers
 * the tallest freshly-added node (a container or branching step with its bottom handles).
 */
const INSERTED_NODE_HEIGHT = 140;
const INSERTED_NODE_GAP = 60;

@customElement("ua-automation-workflow-workspace-view")
export class UaAutomationWorkflowWorkspaceViewElement extends UmbLitElement {
    #workspaceContext?: typeof UA_AUTOMATION_WORKSPACE_CONTEXT.TYPE;
    #catalogueRepository: UaCatalogueRepository;
    #isCanvasUpdate = false;
    /** Bumped per sync so a slower, older sync can't overwrite the nodes of a newer one. */
    #syncGeneration = 0;
    /** Catalogue as of the last canvas rebuild, so edits can look up an action's static outcomes. */
    #catalogueLookup = new Map<string, CatalogueLookupEntry>();
    /** Unreachable step ids as last rendered, to tell when a canvas edit changes them. */
    #lastUnreachableKey = "";

    @state()
    private _nodes: Node[] = [];

    @state()
    private _edges: Edge[] = [];

    @state()
    private _viewport?: Viewport;

    @state()
    private _model?: UaAutomationDetailModel;

    @state()
    private _canvasReady = false;

    #boundEdgeFilterOpen = this.#onEdgeFilterOpen.bind(this);

    constructor() {
        super();
        this.#catalogueRepository = new UaCatalogueRepository(this);

        this.consumeContext(UA_AUTOMATION_WORKSPACE_CONTEXT, (context) => {
            if (!context) return;
            this.#workspaceContext = context;
            this.observe(context.data, (model) => {
                if (!model) return;
                this._model = model;
                if (!this.#isCanvasUpdate) {
                    this.#syncFromModel(model);
                }
            });
        });
    }

    override connectedCallback() {
        super.connectedCallback();
        document.addEventListener("ua:edge-filter-open", this.#boundEdgeFilterOpen as unknown as EventListener);
    }

    override disconnectedCallback() {
        super.disconnectedCallback();
        document.removeEventListener("ua:edge-filter-open", this.#boundEdgeFilterOpen as unknown as EventListener);
    }

    async #syncFromModel(model: UaAutomationDetailModel) {
        const generation = ++this.#syncGeneration;
        const canvasState = this.#parseCanvasState(model.canvasState);
        const catalogue = await this.#buildCatalogueLookup();
        const resolvedOutcomes = await this.#resolveDynamicOutcomes(model.steps, catalogue);
        if (generation !== this.#syncGeneration) return;
        this.#catalogueLookup = catalogue;
        const nodes = modelToNodes(model.trigger, model.steps, canvasState, catalogue, resolvedOutcomes);
        this._nodes = this.#markUnreachableSteps(nodes, model);
        this._edges = modelToEdges(model.connections, getOutcomeDeclaringNodeIds(nodes));
        // Capture saved viewport before the canvas mounts. React Flow's defaultViewport is only
        // honoured on initial render, so the canvas must not mount until this is set; otherwise
        // it falls back to fitView and the saved position is lost.
        if (!this._canvasReady) {
            this._viewport = canvasState?.viewport;
            this._canvasReady = true;
        }
    }

    /**
     * Resolves the outcomes of every plain action step whose action has dynamic outcomes, in
     * parallel (one request per step). A step whose request fails or returns nothing is left out
     * of the map: modelToNodes then marks it as "unknown" and keeps its lines, instead of treating
     * the failure as "no outcomes" (which would make every line look stale).
     */
    async #resolveDynamicOutcomes(
        steps: StepConfigurationModel[],
        catalogue: Map<string, CatalogueLookupEntry>,
    ): Promise<Map<string, UaStepOutcome[]>> {
        const dynamicSteps = steps.filter(
            (s) => isActionNodeType(s.actionAlias) && catalogue.get(s.actionAlias)?.hasDynamicOutcomes === true,
        );
        const results = await Promise.all(
            dynamicSteps.map(async (step) => {
                const { data, error } = await this.#catalogueRepository.resolveOutcomes(step.actionAlias, step.settings);
                return { stepId: step.id, outcomes: error ? undefined : data };
            }),
        );
        const resolved = new Map<string, UaStepOutcome[]>();
        for (const { stepId, outcomes } of results) {
            if (outcomes) resolved.set(stepId, outcomes);
        }
        return resolved;
    }

    /**
     * Flags steps with no path from the trigger so they stand out on the canvas. The compiler drops
     * them and they never run; until now the only signal was the save/publish warning toast.
     * Skipped while there is no trigger, where every step would be flagged and the hint is noise.
     */
    #markUnreachableSteps(nodes: Node[], model: UaAutomationDetailModel): Node[] {
        const unreachable = this.#getUnreachableStepIds(model);
        this.#lastUnreachableKey = [...unreachable].join(",");
        if (unreachable.size === 0) return nodes;
        return nodes.map((node) => (unreachable.has(node.id) ? { ...node, className: "ua-node-unreachable" } : node));
    }

    #getUnreachableStepIds(model: UaAutomationDetailModel): Set<string> {
        if (!model.trigger) return new Set();
        const reachable = computeReachableFromTrigger(model.connections);
        return new Set(model.steps.filter((s) => !reachable.has(s.id)).map((s) => s.id));
    }

    #unreachableKey(model: UaAutomationDetailModel): string {
        return [...this.#getUnreachableStepIds(model)].join(",");
    }

    async #buildCatalogueLookup(): Promise<Map<string, CatalogueLookupEntry>> {
        const lookup = new Map<string, CatalogueLookupEntry>();
        const [triggers, actions, controlFlows] = await Promise.all([
            this.#catalogueRepository.requestTriggers(),
            this.#catalogueRepository.requestActions(),
            this.#catalogueRepository.requestControlFlows(),
        ]);
        for (const t of triggers.data ?? []) {
            lookup.set(t.alias, {
                name: t.name,
                icon: t.icon ?? undefined,
                hasSettings: (t.settingsSchema?.fields?.length ?? 0) > 0,
            });
        }
        for (const a of actions.data ?? []) {
            lookup.set(a.alias, {
                name: a.name,
                icon: a.icon ?? undefined,
                hasSettings: (a.settingsSchema?.fields?.length ?? 0) > 0 || !!a.connectionTypeAlias,
                outcomes: a.outcomes ?? [],
                hasDynamicOutcomes: a.hasDynamicOutcomes ?? false,
            });
        }
        for (const cf of controlFlows.data ?? []) {
            lookup.set(cf.alias, {
                name: cf.name,
                icon: cf.icon ?? undefined,
                hasSettings: (cf.settingsSchema?.fields?.length ?? 0) > 0,
            });
        }
        return lookup;
    }

    #parseCanvasState(json: string | null): CanvasState | null {
        if (!json) return null;
        try {
            return JSON.parse(json) as CanvasState;
        } catch {
            return null;
        }
    }

    #onCanvasChange(event: CustomEvent<CanvasChangeDetail>) {
        const { nodes, edges, viewport } = event.detail;
        if (!this._model) return;

        const steps = flowToSteps(nodes, this._model.steps);
        const connections = flowToConnections(edges);
        const trigger = flowToTrigger(nodes, this._model.trigger);
        // Preserve the trigger's last position so the placeholder reappears in the same spot
        // when the trigger is removed (otherwise it jumps to the default position).
        const previousCanvasState = this.#parseCanvasState(this._model.canvasState);
        const canvasState = JSON.stringify(
            flowToCanvasState(nodes, viewport, previousCanvasState?.triggerPosition),
        );

        const triggerWasRemoved = this._model.trigger !== null && trigger === null;

        this.#isCanvasUpdate = true;
        this.#workspaceContext?.updateProperties({ steps, connections, trigger, canvasState });
        this.#isCanvasUpdate = false;

        // When the trigger is removed via the canvas (Delete key or trash button), the model
        // observer is suppressed by #isCanvasUpdate and the trigger-placeholder is never added
        // back. Force a re-sync so the placeholder reappears and the user can add a replacement.
        // Likewise when a connection drawn or deleted on the canvas changes which steps are
        // unreachable, so their muted styling stays accurate.
        if (this._model && (triggerWasRemoved || this.#unreachableKey(this._model) !== this.#lastUnreachableKey)) {
            this.#syncFromModel(this._model);
        }
    }

    async #onNodeSettingsOpen(event: CustomEvent<NodeSettingsOpenDetail>) {
        const { nodeId, nodeType } = event.detail;
        if (!this._model) return;

        if (nodeType === "trigger") {
            await this.#openTriggerSettingsModal();
        } else {
            await this.#openNodeSettingsModal(nodeId);
        }
    }

    /**
     * Opens the trigger settings modal. Returns `true` when the settings were saved or there was
     * nothing to configure (no settings schema), and `false` when the modal was opened and then
     * dismissed. Callers adding a brand-new trigger use the return value to roll back the add.
     */
    async #openTriggerSettingsModal(): Promise<boolean> {
        if (!this._model?.trigger) return true;

        const catalogueItem = await this.#getTriggerCatalogueItem(this._model.trigger.triggerAlias);
        if (!catalogueItem) return true;

        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return true;

        const modal = modalManager.open(this, UA_TRIGGER_SETTINGS_MODAL, {
            data: {
                automationId: this._model.unique,
                triggerAlias: this._model.trigger.triggerAlias,
                triggerName: catalogueItem.name,
                settings: this._model.trigger.settings,
                schema: catalogueItem.schema,
            },
        });

        try {
            const { settings } = await modal.onSubmit();
            this.#workspaceContext?.updateProperty("trigger", {
                ...this._model!.trigger!,
                settings,
            });
            return true;
        } catch {
            // Modal was dismissed
            return false;
        }
    }

    /**
     * Opens the node (action) settings modal. Returns `true` when the settings were saved or there
     * was nothing to configure (no settings schema), and `false` when the modal was opened and then
     * dismissed. Callers adding a brand-new step use the return value to roll back the add.
     */
    async #openNodeSettingsModal(stepId: string, isNew = false): Promise<boolean> {
        if (!this._model) return true;
        const step = this._model.steps.find((s) => s.id === stepId);
        if (!step) return true;

        const catalogueItem = await this.#getActionCatalogueItem(step.actionAlias);
        if (!catalogueItem) return true;

        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return true;

        const modal = modalManager.open(this, UA_NODE_SETTINGS_MODAL, {
            data: {
                stepId: step.id,
                actionAlias: step.actionAlias,
                actionName: catalogueItem.name,
                name: step.name,
                alias: step.alias ?? null,
                isNew,
                settings: step.settings,
                schema: catalogueItem.schema,
                connectionId: step.connectionId ?? null,
                workspaceId: this._model.workspaceId,
                errorBehavior: step.errorBehavior,
                retryInterval: step.retryInterval ?? null,
                maxRetries: step.maxRetries ?? null,
                automationContext: {
                    trigger: this._model.trigger ?? null,
                    steps: this._model.steps,
                    connections: this._model.connections,
                },
            },
        });

        try {
            const { name, alias, settings, connectionId, errorBehavior, retryInterval, maxRetries } =
                await modal.onSubmit();
            const updatedSteps = this._model.steps.map((s) =>
                s.id === stepId
                    ? { ...s, name, alias, settings, connectionId, errorBehavior, retryInterval, maxRetries }
                    : s,
            );
            this.#workspaceContext?.updateProperty("steps", updatedSteps);
            return true;
        } catch {
            // Modal was dismissed
            return false;
        }
    }

    async #getTriggerCatalogueItem(alias: string): Promise<{ name: string; schema: EditableModelSchemaModel } | null> {
        const { data } = await this.#catalogueRepository.requestTriggers();
        const trigger = data?.find((t) => t.alias === alias);
        if (!trigger?.settingsSchema) return null;
        return { name: trigger.name, schema: trigger.settingsSchema };
    }

    async #getActionCatalogueItem(alias: string): Promise<{ name: string; schema: EditableModelSchemaModel } | null> {
        // Check actions first, then control flows
        const { data: actions } = await this.#catalogueRepository.requestActions();
        const action = actions?.find((a) => a.alias === alias);
        if (action) return { name: action.name, schema: action.settingsSchema ?? { fields: [] } };

        const { data: controlFlows } = await this.#catalogueRepository.requestControlFlows();
        const cf = controlFlows?.find((c) => c.alias === alias);
        if (cf?.settingsSchema) return { name: cf.name, schema: cf.settingsSchema };

        return null;
    }

    async #onNodeDeleteRequest(event: CustomEvent<NodeDeleteRequestDetail>) {
        const { nodes, resolve } = event.detail;
        if (nodes.length === 0) {
            resolve(true);
            return;
        }

        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) {
            resolve(false);
            return;
        }

        const label = nodes.length === 1
            ? (nodes[0].data as { label?: string }).label ?? nodes[0].type ?? "node"
            : `${nodes.length} nodes`;

        const modal = modalManager.open(this, UMB_CONFIRM_MODAL, {
            data: {
                headline: this.localize.term("uaGeneral_delete"),
                content: this.localize.term("uaCanvas_nodeDeleteConfirm", label),
                color: "danger",
                confirmLabel: this.localize.term("uaGeneral_delete"),
            },
        });

        try {
            await modal.onSubmit();
            resolve(true);
        } catch {
            resolve(false);
        }
    }

    async #onAddNodeRequest(event: CustomEvent<AddNodeRequestDetail>) {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;
        const modal = modalManager.open(this, UA_NODE_PICKER_MODAL, {
            data: { mode: "action", workspaceId: this._model?.workspaceId },
        });

        try {
            const { item } = await modal.onSubmit();
            if (!item || !this._model) return;

            // Resolve a dynamic action's outcomes before reading any model state, so the awaited
            // request can't leave the steps/connections built below stale (e.g. a node dragged
            // meanwhile). A new step has no settings yet, hence {}.
            const catalogueEntry = this.#catalogueLookup.get(item.alias);
            let newStepOutcomes = catalogueEntry?.outcomes;
            if (catalogueEntry?.hasDynamicOutcomes) {
                const resolved = await this.#catalogueRepository.resolveOutcomes(item.alias, {});
                newStepOutcomes = resolved.error ? undefined : (resolved.data ?? undefined);
            }
            if (!this._model) return;

            // Snapshot state before inserting so we can roll back the whole add (step plus any
            // auto-connected/spliced edges) if the settings modal is closed without saving.
            const previousSteps = this._model.steps;
            const previousConnections = this._model.connections;

            // "+" on an output that already has a connection splices the new step in front of the
            // existing target rather than replacing that connection. Replacing it used to orphan
            // the old target: an unreachable step that silently never runs.
            const connectedInsert = this.#resolveConnectedHandleInsert(event.detail.connectFrom);
            const insertBetween = this.#normaliseInsertBetween(event.detail.insertBetween) ?? connectedInsert?.insertBetween;

            const newStepId = crypto.randomUUID();
            const newStep = {
                id: newStepId,
                actionAlias: item.alias,
                name: item.name,
                alias: this.#generateStepAlias(item.alias),
                connectionId: null,
                settings: {},
                inputMappings: {},
                // From the "+" button, a connected-handle insert takes the existing target's slot
                // (the target and everything below it shift down to make room). A drag dropped on
                // the pane keeps the position the user chose.
                position: (event.detail.autoPosition ? connectedInsert?.targetPosition : undefined) ?? event.detail.position,
                errorBehavior: "Terminate" as const,
                retryInterval: null,
                maxRetries: null,
            };

            const updatedSteps = [...this._model.steps, newStep];

            if (insertBetween) {
                // Splice the new step onto an existing edge: A→B becomes A→new→B.
                // Preserve the original edge's outcome and filter on the upstream half so
                // branch labels and conditions stay attached to the source.
                const { sourceStepId, sourceHandle, targetStepId, targetHandle } = insertBetween;
                const normalisedSource = sourceStepId === TRIGGER_NODE_ID ? UA_EMPTY_GUID : sourceStepId;
                // The downstream half leaves the new step through the handle that continues the
                // original flow. A null handle only suits plain actions: on a container it would be
                // read as a body edge, and If/Switch/Approval have no unnamed output at all.
                //
                // A dynamic action's static list is empty, so resolve it first. If that fails, or
                // the step genuinely has no exits yet (e.g. nothing configured), the handle is
                // null and the downstream line stays unnamed, shown as "Any result". That keeps the
                // line rather than dropping it; the user moves it once the step has exits.
                const continuationHandle = getContinuationSourceHandle(
                    newStep.actionAlias,
                    newStep.settings,
                    newStepOutcomes,
                );
                const updatedConnections = this._model.connections.flatMap((conn) => {
                    const matchesSource = conn.sourceStepId === normalisedSource
                        && (conn.sourceHandle ?? null) === (sourceHandle ?? null);
                    const matchesTarget = conn.targetStepId === targetStepId
                        && (conn.targetHandle ?? null) === (targetHandle ?? null);
                    if (!matchesSource || !matchesTarget) return [conn];
                    return [
                        { ...conn, targetStepId: newStepId, targetHandle: null },
                        {
                            sourceStepId: newStepId,
                            sourceHandle: continuationHandle,
                            targetStepId,
                            targetHandle: targetHandle ?? null,
                            outcome: continuationHandle,
                            filter: null,
                        },
                    ];
                });
                const shiftedSteps = this.#shiftDownstreamSteps(updatedSteps, updatedConnections, newStep, normalisedSource, targetStepId);
                this.#workspaceContext?.updateProperties({
                    steps: this.#moveClearOfOtherSteps(shiftedSteps, newStepId),
                    connections: updatedConnections,
                });
            } else if (event.detail.connectFrom) {
                // Auto-connect a free output (or a further Parallel branch) to the new step.
                const { sourceStepId } = event.detail.connectFrom;
                // "__any__" is display-only and must never be saved as a handle or outcome.
                const sourceHandle = event.detail.connectFrom.sourceHandle === ANY_RESULT_HANDLE
                    ? null
                    : event.detail.connectFrom.sourceHandle;
                const normalisedSourceId = sourceStepId === TRIGGER_NODE_ID ? UA_EMPTY_GUID : sourceStepId;
                const newConnection = {
                    sourceStepId: normalisedSourceId,
                    sourceHandle: sourceHandle ?? null,
                    targetStepId: newStepId,
                    targetHandle: null,
                    outcome: sourceHandle ?? null,
                    filter: null,
                };
                this.#workspaceContext?.updateProperties({
                    steps: updatedSteps,
                    connections: [...this._model.connections, newConnection],
                });
            } else {
                this.#workspaceContext?.updateProperty("steps", updatedSteps);
            }

            const saved = await this.#openNodeSettingsModal(newStepId, true);
            if (!saved) {
                // Settings modal closed without saving: discard the just-added step and restore
                // any connections the add rewired.
                this.#workspaceContext?.updateProperties({
                    steps: previousSteps,
                    connections: previousConnections,
                });
            }
        } catch {
            // Modal was dismissed
        }
    }

    /** "Any result" is a display-only handle; the saved unnamed connection has a null handle. */
    #normaliseInsertBetween(
        insertBetween: AddNodeRequestDetail["insertBetween"],
    ): AddNodeRequestDetail["insertBetween"] {
        if (insertBetween?.sourceHandle !== ANY_RESULT_HANDLE) return insertBetween;
        return { ...insertBetween, sourceHandle: null };
    }

    async #onAddTrigger() {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;
        const modal = modalManager.open(this, UA_NODE_PICKER_MODAL, {
            data: { mode: "trigger", workspaceId: this._model?.workspaceId },
        });

        try {
            const { item } = await modal.onSubmit();
            if (!item) return;

            // Snapshot the trigger (null when adding to the placeholder) so we can roll back if the
            // settings modal is closed without saving.
            const previousTrigger = this._model?.trigger ?? null;

            this.#workspaceContext?.updateProperty("trigger", {
                triggerAlias: item.alias,
                settings: {},
            });

            const saved = await this.#openTriggerSettingsModal();
            if (!saved) {
                // Settings modal closed without saving: discard the just-added trigger.
                this.#workspaceContext?.updateProperty("trigger", previousTrigger);
            }
        } catch {
            // Modal was dismissed
        }
    }

    /**
     * Generates a unique step alias from an action alias.
     * Extracts the last segment (e.g. "umbracoAutomate.httpRequest" → "httpRequest")
     * and appends an incrementing number if the base name is already used.
     */
    #generateStepAlias(actionAlias: string): string {
        const lastDot = actionAlias.lastIndexOf(".");
        const baseName = lastDot >= 0 ? actionAlias.substring(lastDot + 1) : actionAlias;

        const usedAliases = new Set(
            (this._model?.steps ?? [])
                .map((s) => s.alias?.toLowerCase())
                .filter(Boolean),
        );

        if (!usedAliases.has(baseName.toLowerCase())) {
            return baseName;
        }

        for (let i = 2; i < 1000; i++) {
            const candidate = `${baseName}${i}`;
            if (!usedAliases.has(candidate.toLowerCase())) {
                return candidate;
            }
        }

        return `${baseName}${Date.now()}`;
    }

    /**
     * Makes room for a step spliced into an existing connection. The new step is placed at the
     * connection's midpoint, which is usually closer to the downstream step than a node is tall,
     * so the downstream step and everything reachable from it move down together until it sits
     * below the new step. Nodes upstream of the insert point never move.
     */
    #shiftDownstreamSteps(
        steps: StepConfigurationModel[],
        connections: StepConnectionModel[],
        insertedStep: StepConfigurationModel,
        upstreamStepId: string,
        downstreamStepId: string,
    ): StepConfigurationModel[] {
        const downstreamStep = steps.find((s) => s.id === downstreamStepId);
        if (!downstreamStep) return steps;

        const requiredTop = insertedStep.position.y + INSERTED_NODE_HEIGHT + INSERTED_NODE_GAP;
        const shift = requiredTop - downstreamStep.position.y;
        if (shift <= 0) return steps;

        // Walk from the downstream step. The inserted step and the step it was inserted after are
        // excluded, so a cycle back up the graph cannot drag them (or anything above them) down.
        const toShift = new Set<string>([downstreamStepId]);
        const queue = [downstreamStepId];
        for (let i = 0; i < queue.length; i++) {
            for (const conn of connections) {
                if (conn.sourceStepId !== queue[i]) continue;
                const target = conn.targetStepId;
                if (target === insertedStep.id || target === upstreamStepId || toShift.has(target)) continue;
                toShift.add(target);
                queue.push(target);
            }
        }

        return steps.map((s) =>
            toShift.has(s.id) ? { ...s, position: { ...s.position, y: s.position.y + shift } } : s,
        );
    }

    /**
     * When "+" is clicked on an output that already carries a connection, returns that connection
     * as an insert point plus the current target's position. Returns undefined for a free output,
     * and for a Parallel body handle, which always adds a new branch alongside the existing ones.
     */
    #resolveConnectedHandleInsert(connectFrom: AddNodeRequestDetail["connectFrom"]):
        | { insertBetween: NonNullable<AddNodeRequestDetail["insertBetween"]>; targetPosition: { x: number; y: number } }
        | undefined {
        if (!connectFrom || !this._model) return undefined;
        const sourceHandle = connectFrom.sourceHandle === ANY_RESULT_HANDLE ? null : (connectFrom.sourceHandle ?? null);
        if (this.#isParallelBranchHandle(connectFrom.sourceStepId, sourceHandle)) return undefined;

        const normalisedSource = connectFrom.sourceStepId === TRIGGER_NODE_ID ? UA_EMPTY_GUID : connectFrom.sourceStepId;
        const existing = this._model.connections.find(
            (c) => c.sourceStepId === normalisedSource && (c.sourceHandle ?? null) === sourceHandle,
        );
        if (!existing) return undefined;

        const target = this._model.steps.find((s) => s.id === existing.targetStepId);
        if (!target) return undefined;

        return {
            insertBetween: {
                sourceStepId: connectFrom.sourceStepId,
                sourceHandle,
                targetStepId: existing.targetStepId,
                targetHandle: existing.targetHandle ?? null,
            },
            targetPosition: { x: target.position.x, y: target.position.y },
        };
    }

    /**
     * Nudges a spliced-in step sideways until it clears every other step. An edge insert lands at
     * the connection's midpoint, which can sit on a sibling branch, and the downstream shift only
     * moves steps below it. Saved positions carry no measured size, so default sizes are used.
     */
    #moveClearOfOtherSteps(steps: StepConfigurationModel[], stepId: string): StepConfigurationModel[] {
        const step = steps.find((s) => s.id === stepId);
        if (!step) return steps;

        const obstacles = steps
            .filter((s) => s.id !== stepId)
            .map((s) => ({ x: s.position.x, y: s.position.y, width: DEFAULT_NODE_WIDTH, height: DEFAULT_NODE_HEIGHT }));
        const position = findFreePosition(
            step.position,
            { width: DEFAULT_NODE_WIDTH, height: INSERTED_NODE_HEIGHT },
            { x: DEFAULT_NODE_WIDTH + NEW_NODE_GAP, y: 0 },
            obstacles,
        );
        if (position.x === step.position.x && position.y === step.position.y) return steps;
        return steps.map((s) => (s.id === stepId ? { ...s, position } : s));
    }

    /**
     * Parallel's body handle fans out to many branches, so unlike every other handle (a single
     * outgoing edge, e.g. done, or an If/Switch case) it must accept more than one connection.
     */
    #isParallelBranchHandle(sourceStepId: string, sourceHandle: string | null): boolean {
        if (sourceHandle !== BODY_HANDLE) return false;
        const sourceStep = this._model?.steps.find((s) => s.id === sourceStepId);
        return sourceStep?.actionAlias === PARALLEL_ALIAS;
    }

    async #onEdgeFilterOpen(event: CustomEvent<EdgeFilterOpenDetail>) {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager || !this._model) return;

        const { source, target, targetHandle, filter } = event.detail;
        const sourceHandle = event.detail.sourceHandle === ANY_RESULT_HANDLE ? null : event.detail.sourceHandle;

        const modal = modalManager.open(this, UA_EDGE_FILTER_MODAL, {
            data: {
                filter,
                targetStepId: target,
                automationContext: {
                    trigger: this._model.trigger ?? null,
                    steps: this._model.steps,
                    connections: this._model.connections,
                },
            },
        });

        try {
            const { filter: updatedFilter } = await modal.onSubmit();

            const normalisedSource = source === TRIGGER_NODE_ID ? UA_EMPTY_GUID : source;
            const updatedConnections = this._model.connections.map((conn) => {
                if (
                    conn.sourceStepId === normalisedSource &&
                    (conn.sourceHandle ?? null) === (sourceHandle ?? null) &&
                    conn.targetStepId === target &&
                    (conn.targetHandle ?? null) === (targetHandle ?? null)
                ) {
                    return { ...conn, filter: updatedFilter };
                }
                return conn;
            });

            this.#workspaceContext?.updateProperty("connections", updatedConnections);
        } catch {
            // Modal was dismissed
        }
    }

    override render() {
        return html`
            <div id="canvas">
                ${this._canvasReady
                    ? html`<ua-automation-canvas
                          .nodes=${this._nodes}
                          .edges=${this._edges}
                          .viewport=${this._viewport}
                          @ua:canvas-change=${this.#onCanvasChange}
                          @ua:add-node-request=${this.#onAddNodeRequest}
                          @ua:add-trigger-request=${this.#onAddTrigger}
                          @ua:node-settings-open=${this.#onNodeSettingsOpen}
                          @ua:node-delete-request=${this.#onNodeDeleteRequest}
                      ></ua-automation-canvas>`
                    : html`<uui-loader></uui-loader>`}
            </div>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: flex;
                flex-direction: column;
                height: 100%;
            }

            #canvas {
                flex: 1;
                min-height: 0;
            }
        `,
    ];
}

export default UaAutomationWorkflowWorkspaceViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-workflow-workspace-view": UaAutomationWorkflowWorkspaceViewElement;
    }
}
