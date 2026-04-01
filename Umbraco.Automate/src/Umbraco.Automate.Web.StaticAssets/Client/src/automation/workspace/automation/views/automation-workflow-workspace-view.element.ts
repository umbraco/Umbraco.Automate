import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_MODAL_MANAGER_CONTEXT, UMB_CONFIRM_MODAL } from "@umbraco-cms/backoffice/modal";
import type { Node, Edge, Viewport } from "@xyflow/react";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import type { UaAutomationDetailModel } from "../../../types.js";
import { modelToNodes, modelToEdges } from "../canvas/utils/model-to-flow.js";
import { flowToSteps, flowToConnections, flowToCanvasState, flowToTrigger } from "../canvas/utils/flow-to-model.js";
import type { CanvasState, CanvasChangeDetail, AddNodeRequestDetail, NodeSettingsOpenDetail, NodeDeleteRequestDetail } from "../canvas/types.js";
import { UA_NODE_PICKER_MODAL } from "../../../../catalogue/modals/node-picker/node-picker-modal.token.js";
import { UA_NODE_SETTINGS_MODAL } from "../../../modals/node-settings/node-settings-modal.token.js";
import { UA_TRIGGER_SETTINGS_MODAL } from "../../../modals/trigger-settings/trigger-settings-modal.token.js";
import { UaCatalogueRepository } from "../../../../catalogue/repository/catalogue.repository.js";
import type { EditableModelSchemaModel } from "../../../../api/types.gen.js";
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
        const [triggers, actions] = await Promise.all([
            this.#catalogueRepository.requestTriggers(),
            this.#catalogueRepository.requestActions(),
        ]);
        for (const t of triggers.data ?? []) {
            names.set(t.alias, t.name);
        }
        for (const a of actions.data ?? []) {
            names.set(a.alias, a.name);
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
        const { data } = await this.#catalogueRepository.requestActions();
        const action = data?.find((a) => a.alias === alias);
        if (!action?.settingsSchema) return null;
        return { name: action.name, schema: action.settingsSchema };
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
            data: { mode: "action" },
        });

        try {
            const { item } = await modal.onSubmit();
            if (!item || !this._model) return;

            const newStepId = crypto.randomUUID();
            const newStep = {
                id: newStepId,
                actionAlias: item.alias,
                name: item.name,
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
        } catch {
            // Modal was dismissed
        }
    }

    async #onAddTrigger() {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;
        const modal = modalManager.open(this, UA_NODE_PICKER_MODAL, {
            data: { mode: "trigger" },
        });

        try {
            const { item } = await modal.onSubmit();
            if (!item) return;

            this.#workspaceContext?.updateProperty("trigger", {
                triggerAlias: item.alias,
                settings: {},
            });
        } catch {
            // Modal was dismissed
        }
    }

    async #onAddAction() {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;
        const modal = modalManager.open(this, UA_NODE_PICKER_MODAL, {
            data: { mode: "action" },
        });

        try {
            const { item } = await modal.onSubmit();
            if (!item || !this._model) return;

            const lastNode = this._nodes[this._nodes.length - 1];
            const position = lastNode
                ? { x: lastNode.position.x, y: lastNode.position.y + 150 }
                : { x: 250, y: 200 };

            const newStep = {
                id: crypto.randomUUID(),
                actionAlias: item.alias,
                name: item.name,
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
        } catch {
            // Modal was dismissed
        }
    }

    override render() {
        return html`
            <div id="toolbar">
                ${!this._model?.trigger
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
                </uui-button>
            </div>
            <div id="canvas">
                <ua-automation-canvas
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
        `,
    ];
}

export default UaAutomationWorkflowWorkspaceViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-workflow-workspace-view": UaAutomationWorkflowWorkspaceViewElement;
    }
}
