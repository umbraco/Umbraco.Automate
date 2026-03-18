import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_MODAL_MANAGER_CONTEXT } from "@umbraco-cms/backoffice/modal";
import type { Node, Edge, Viewport } from "@xyflow/react";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import type { UaAutomationDetailModel } from "../../../types.js";
import { modelToNodes, modelToEdges } from "../canvas/utils/model-to-flow.js";
import { flowToSteps, flowToConnections, flowToCanvasState, flowToTrigger } from "../canvas/utils/flow-to-model.js";
import type { CanvasState, CanvasChangeDetail, AddNodeRequestDetail, NodeSettingsOpenDetail } from "../canvas/types.js";
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

    #syncFromModel(model: UaAutomationDetailModel) {
        const canvasState = this.#parseCanvasState(model.canvasState);
        this._nodes = modelToNodes(model.trigger, model.steps, canvasState);
        this._edges = modelToEdges(model.connections);
        this._viewport = canvasState?.viewport;
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

        const schema = await this.#getTriggerSchema(this._model.trigger.triggerAlias);
        if (!schema) return;

        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;

        const modal = modalManager.open(this, UA_TRIGGER_SETTINGS_MODAL, {
            data: {
                triggerAlias: this._model.trigger.triggerAlias,
                settings: this._model.trigger.settings,
                schema,
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

        const schema = await this.#getActionSchema(step.actionAlias);
        if (!schema) return;

        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;

        const modal = modalManager.open(this, UA_NODE_SETTINGS_MODAL, {
            data: {
                stepId: step.id,
                actionAlias: step.actionAlias,
                settings: step.settings,
                schema,
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

    async #getTriggerSchema(alias: string): Promise<EditableModelSchemaModel | null> {
        const { data } = await this.#catalogueRepository.requestTriggers();
        const trigger = data?.find((t) => t.alias === alias);
        return trigger?.settingsSchema ?? null;
    }

    async #getActionSchema(alias: string): Promise<EditableModelSchemaModel | null> {
        const { data } = await this.#catalogueRepository.requestActions();
        const action = data?.find((a) => a.alias === alias);
        return action?.settingsSchema ?? null;
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
                    <uui-icon name="icon-add"></uui-icon>
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
