import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_MODAL_MANAGER_CONTEXT, UMB_CONFIRM_MODAL } from "@umbraco-cms/backoffice/modal";
import type { Node, Edge, Viewport } from "@xyflow/react";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import type { UaAutomationDetailModel } from "../../../types.js";
import { modelToNodes, modelToEdges, TRIGGER_NODE_ID } from "../canvas/utils/model-to-flow.js";
import { flowToSteps, flowToConnections, flowToCanvasState, flowToTrigger } from "../canvas/utils/flow-to-model.js";
import type { CanvasState, CanvasChangeDetail, CatalogueLookupEntry, AddNodeRequestDetail, NodeSettingsOpenDetail, NodeDeleteRequestDetail, EdgeFilterOpenDetail } from "../canvas/types.js";
import { UA_NODE_PICKER_MODAL } from "../../../../catalogue/modals/node-picker/node-picker-modal.token.js";
import { UA_NODE_SETTINGS_MODAL } from "../../../modals/node-settings/node-settings-modal.token.js";
import { UA_TRIGGER_SETTINGS_MODAL } from "../../../modals/trigger-settings/trigger-settings-modal.token.js";
import { UA_EDGE_FILTER_MODAL } from "../../../modals/edge-filter/edge-filter-modal.token.js";
import { UaCatalogueRepository } from "../../../../catalogue/repository/catalogue.repository.js";
import type { EditableModelSchemaModel } from "../../../../api/types.gen.js";
import { UA_EMPTY_GUID } from "../../../../core/index.js";
import "../canvas/ua-automation-canvas.element.js";

@customElement("ua-automation-workflow-workspace-view")
export class UaAutomationWorkflowWorkspaceViewElement extends UmbLitElement {
    #workspaceContext?: typeof UA_AUTOMATION_WORKSPACE_CONTEXT.TYPE;
    #catalogueRepository: UaCatalogueRepository;
    #isCanvasUpdate = false;

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
        const canvasState = this.#parseCanvasState(model.canvasState);
        const catalogue = await this.#buildCatalogueLookup();
        this._nodes = modelToNodes(model.trigger, model.steps, canvasState, catalogue);
        this._edges = modelToEdges(model.connections);
        // Capture saved viewport before the canvas mounts. React Flow's defaultViewport is only
        // honoured on initial render, so the canvas must not mount until this is set; otherwise
        // it falls back to fitView and the saved position is lost.
        if (!this._canvasReady) {
            this._viewport = canvasState?.viewport;
            this._canvasReady = true;
        }
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
                hasSettings: (a.settingsSchema?.fields?.length ?? 0) > 0,
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
        if (triggerWasRemoved && this._model) {
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

    async #openTriggerSettingsModal() {
        if (!this._model?.trigger) return;

        const catalogueItem = await this.#getTriggerCatalogueItem(this._model.trigger.triggerAlias);
        if (!catalogueItem) return;

        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;

        const modal = modalManager.open(this, UA_TRIGGER_SETTINGS_MODAL, {
            data: {
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
        } catch {
            // Modal was dismissed
        }
    }

    async #openNodeSettingsModal(stepId: string) {
        if (!this._model) return;
        const step = this._model.steps.find((s) => s.id === stepId);
        if (!step) return;

        const catalogueItem = await this.#getActionCatalogueItem(step.actionAlias);
        if (!catalogueItem) return;

        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;

        const modal = modalManager.open(this, UA_NODE_SETTINGS_MODAL, {
            data: {
                stepId: step.id,
                actionAlias: step.actionAlias,
                actionName: catalogueItem.name,
                settings: step.settings,
                schema: catalogueItem.schema,
                connectionId: step.connectionId ?? null,
                workspaceId: this._model.workspaceId,
                automationContext: {
                    trigger: this._model.trigger ?? null,
                    steps: this._model.steps,
                    connections: this._model.connections,
                },
            },
        });

        try {
            const { settings, connectionId } = await modal.onSubmit();
            const updatedSteps = this._model.steps.map((s) =>
                s.id === stepId ? { ...s, settings, connectionId } : s,
            );
            this.#workspaceContext?.updateProperty("steps", updatedSteps);
        } catch {
            // Modal was dismissed
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
        if (action?.settingsSchema) return { name: action.name, schema: action.settingsSchema };

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

            const newStepId = crypto.randomUUID();
            const newStep = {
                id: newStepId,
                actionAlias: item.alias,
                name: item.name,
                alias: this.#generateStepAlias(item.alias),
                connectionId: null,
                settings: {},
                inputMappings: {},
                position: event.detail.position,
                errorBehavior: "Terminate" as const,
                retryInterval: null,
                maxRetries: null,
            };

            const updatedSteps = [...this._model.steps, newStep];
            this.#workspaceContext?.updateProperty("steps", updatedSteps);

            // Auto-connect when the node was created by dragging from a handle.
            if (event.detail.connectFrom) {
                const { sourceStepId, sourceHandle } = event.detail.connectFrom;
                const newConnection = {
                    sourceStepId: sourceStepId === TRIGGER_NODE_ID ? UA_EMPTY_GUID : sourceStepId,
                    sourceHandle: sourceHandle ?? null,
                    targetStepId: newStepId,
                    targetHandle: null,
                    outcome: sourceHandle ?? null,
                    filter: null,
                };
                const updatedConnections = [...this._model.connections, newConnection];
                this.#workspaceContext?.updateProperty("connections", updatedConnections);
            } else if (event.detail.insertBetween) {
                // Splice the new step onto an existing edge: A→B becomes A→new→B.
                // Preserve the original edge's outcome and filter on the upstream half so
                // branch labels and conditions stay attached to the source.
                const { sourceStepId, sourceHandle, targetStepId, targetHandle } = event.detail.insertBetween;
                const normalisedSource = sourceStepId === TRIGGER_NODE_ID ? UA_EMPTY_GUID : sourceStepId;
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
                            sourceHandle: null,
                            targetStepId,
                            targetHandle: targetHandle ?? null,
                            outcome: null,
                            filter: null,
                        },
                    ];
                });
                this.#workspaceContext?.updateProperty("connections", updatedConnections);
            }

            await this.#openNodeSettingsModal(newStepId);
        } catch {
            // Modal was dismissed
        }
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

            this.#workspaceContext?.updateProperty("trigger", {
                triggerAlias: item.alias,
                settings: {},
            });

            await this.#openTriggerSettingsModal();
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

    async #onEdgeFilterOpen(event: CustomEvent<EdgeFilterOpenDetail>) {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager || !this._model) return;

        const { source, sourceHandle, target, targetHandle, filter } = event.detail;

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
