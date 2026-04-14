import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_MODAL_MANAGER_CONTEXT, UMB_CONFIRM_MODAL } from "@umbraco-cms/backoffice/modal";
import type { Node, Edge, Viewport } from "@xyflow/react";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import type { UaAutomationDetailModel } from "../../../types.js";
import { modelToNodes, modelToEdges } from "../canvas/utils/model-to-flow.js";
import { flowToSteps, flowToConnections, flowToCanvasState, flowToTrigger } from "../canvas/utils/flow-to-model.js";
import type { CanvasState, CanvasChangeDetail, AddNodeRequestDetail, NodeSettingsOpenDetail, NodeDeleteRequestDetail, EdgeFilterOpenDetail } from "../canvas/types.js";
import { UA_NODE_PICKER_MODAL } from "../../../../catalogue/modals/node-picker/node-picker-modal.token.js";
import { UA_NODE_SETTINGS_MODAL } from "../../../modals/node-settings/node-settings-modal.token.js";
import { UA_TRIGGER_SETTINGS_MODAL } from "../../../modals/trigger-settings/trigger-settings-modal.token.js";
import { UA_EDGE_FILTER_MODAL } from "../../../modals/edge-filter/edge-filter-modal.token.js";
import { UaCatalogueRepository } from "../../../../catalogue/repository/catalogue.repository.js";
import type { AutomationRunResponseModel, EditableModelSchemaModel } from "../../../../api/types.gen.js";
import "../canvas/ua-automation-canvas.element.js";

const TRIGGER_NODE_ID = "__trigger__";
const DRY_RUN_OVERLAY_DURATION_MS = 8000;

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
    private _dryRunActive = false;

    @state()
    private _dryRunRun?: AutomationRunResponseModel;

    #dryRunTimer?: ReturnType<typeof setTimeout>;
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
                if (!this.#isCanvasUpdate && !this._dryRunActive) {
                    this.#syncFromModel(model);
                }
            });
            this.observe(context.dryRunResult, (run) => {
                this._dryRunRun = run ?? undefined;
                this._dryRunActive = !!run;
                if (run) {
                    this.#applyDryRunOverlay(run);
                    this.#scheduleDryRunClear();
                } else if (this._model) {
                    this.#syncFromModel(this._model);
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
        clearTimeout(this.#dryRunTimer);
    }

    #applyDryRunOverlay(run: AutomationRunResponseModel) {
        this._nodes = this._nodes.map((node) => {
            if (node.id === TRIGGER_NODE_ID) {
                const triggerStatus =
                    run.status === "Failed" && run.stepRuns.length === 0 ? "Failed" : "Completed";
                return {
                    ...node,
                    className: `run-status-${triggerStatus.toLowerCase()}`,
                    data: { ...node.data, runStatus: triggerStatus },
                };
            }

            const stepRun = run.stepRuns.find((sr) => sr.stepId === node.id);
            const status = stepRun?.status ?? "Pending";
            return {
                ...node,
                className: `run-status-${status.toLowerCase()}`,
                data: { ...node.data, runStatus: status, stepRun },
            };
        });
    }

    #scheduleDryRunClear() {
        clearTimeout(this.#dryRunTimer);
        this.#dryRunTimer = setTimeout(() => {
            this.#workspaceContext?.clearDryRunOverlay();
        }, DRY_RUN_OVERLAY_DURATION_MS);
    }

    #dismissDryRun() {
        clearTimeout(this.#dryRunTimer);
        this.#workspaceContext?.clearDryRunOverlay();
    }

    async #syncFromModel(model: UaAutomationDetailModel) {
        const canvasState = this.#parseCanvasState(model.canvasState);
        const catalogueNames = await this.#buildCatalogueNames();
        this._nodes = modelToNodes(model.trigger, model.steps, canvasState, catalogueNames);
        this._edges = modelToEdges(model.connections);
        // Only set viewport on initial load; after that the canvas manages its own position
        if (!this._viewport) {
            this._viewport = canvasState?.viewport;
        }
    }

    async #buildCatalogueNames(): Promise<Map<string, string>> {
        const names = new Map<string, string>();
        const [triggers, actions, controlFlows] = await Promise.all([
            this.#catalogueRepository.requestTriggers(),
            this.#catalogueRepository.requestActions(),
            this.#catalogueRepository.requestControlFlows(),
        ]);
        for (const t of triggers.data ?? []) {
            names.set(t.alias, t.name);
        }
        for (const a of actions.data ?? []) {
            names.set(a.alias, a.name);
        }
        for (const cf of controlFlows.data ?? []) {
            names.set(cf.alias, cf.name);
        }
        return names;
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
        const canvasState = JSON.stringify(flowToCanvasState(nodes, viewport));

        this.#isCanvasUpdate = true;
        this.#workspaceContext?.updateProperties({ steps, connections, trigger, canvasState });
        this.#isCanvasUpdate = false;
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
                automationContext: {
                    trigger: this._model.trigger ?? null,
                    steps: this._model.steps,
                    connections: this._model.connections,
                },
            },
        });

        try {
            const { settings } = await modal.onSubmit();
            const updatedSteps = this._model.steps.map((s) =>
                s.id === stepId ? { ...s, settings } : s,
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

    async #onAddAction() {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;
        const modal = modalManager.open(this, UA_NODE_PICKER_MODAL, {
            data: { mode: "action", workspaceId: this._model?.workspaceId },
        });

        try {
            const { item } = await modal.onSubmit();
            if (!item || !this._model) return;

            const lastNode = this._nodes[this._nodes.length - 1];
            const position = lastNode
                ? { x: lastNode.position.x, y: lastNode.position.y + 150 }
                : { x: 250, y: 200 };

            const newStepId = crypto.randomUUID();
            const newStep = {
                id: newStepId,
                actionAlias: item.alias,
                name: item.name,
                alias: this.#generateStepAlias(item.alias),
                connectionId: null,
                settings: {},
                inputMappings: {},
                position,
                errorBehavior: "Terminate" as const,
                retryInterval: null,
                maxRetries: null,
            };

            const updatedSteps = [...this._model.steps, newStep];
            this.#workspaceContext?.updateProperty("steps", updatedSteps);

            await this.#openNodeSettingsModal(newStepId);
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

        const { edgeId, filter } = event.detail;

        const modal = modalManager.open(this, UA_EDGE_FILTER_MODAL, {
            data: { filter },
        });

        try {
            const { filter: updatedFilter } = await modal.onSubmit();

            // Update the connection's filter
            const updatedConnections = this._model.connections.map((conn) => {
                const connEdgeId = `${conn.sourceStepId}-${conn.sourceHandle ?? "default"}-${conn.targetStepId}-${conn.targetHandle ?? "default"}`;
                if (connEdgeId === edgeId) {
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
            <div id="toolbar">
                ${this._dryRunActive
                    ? html`<div class="dry-run-banner">
                          <uui-icon name="icon-lab"></uui-icon>
                          <span
                              >Dry run
                              ${this._dryRunRun?.status === "Completed"
                                  ? "completed"
                                  : "failed"}</span
                          >
                          ${this._dryRunRun?.error
                              ? html`<small>${this._dryRunRun.error}</small>`
                              : ""}
                          <uui-button
                              look="outline"
                              compact
                              label="Dismiss"
                              @click=${this.#dismissDryRun}
                          ></uui-button>
                      </div>`
                    : html`${!this._model?.trigger
                          ? html`<uui-button
                                look="outline"
                                color="positive"
                                label=${this.localize.term("uaCatalogue_selectTrigger")}
                                @click=${this.#onAddTrigger}
                            >
                                <uui-icon name="icon-flash"></uui-icon>
                                Add Trigger
                            </uui-button>`
                          : ""}
                      <uui-button
                          look="outline"
                          label=${this.localize.term("uaCatalogue_selectAction")}
                          @click=${this.#onAddAction}
                      >
                          <uui-icon name="icon-circuits"></uui-icon>
                          Add Action
                      </uui-button>`}
            </div>
            <div id="canvas">
                <ua-automation-canvas
                    ?read-only=${this._dryRunActive}
                    .nodes=${this._nodes}
                    .edges=${this._edges}
                    .viewport=${this._viewport}
                    @ua:canvas-change=${this.#onCanvasChange}
                    @ua:add-node-request=${this.#onAddNodeRequest}
                    @ua:node-settings-open=${this.#onNodeSettingsOpen}
                    @ua:node-delete-request=${this.#onNodeDeleteRequest}
                ></ua-automation-canvas>
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

            #toolbar {
                display: flex;
                gap: var(--uui-size-space-3);
                padding: var(--uui-size-space-3) var(--uui-size-space-4);
                border-bottom: 1px solid var(--uui-color-border);
                background: var(--uui-color-surface);
            }

            #canvas {
                flex: 1;
                min-height: 0;
            }

            .dry-run-banner {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-3);
                flex: 1;
                font-weight: 600;
            }

            .dry-run-banner small {
                font-weight: 400;
                opacity: 0.8;
            }

            .dry-run-banner uui-button {
                margin-left: auto;
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
