import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { Node, Edge } from "@xyflow/react";
import { UA_RUN_WORKSPACE_CONTEXT } from "../run-workspace.context-token.js";
import type { UaRunDetailModel } from "../../../types.js";
import type { UaAutomationDetailModel } from "../../../../automation/types.js";
import { modelToNodes, modelToEdges } from "../../../../automation/workspace/automation/canvas/utils/model-to-flow.js";
import type { CanvasState } from "../../../../automation/workspace/automation/canvas/types.js";
import { UaCatalogueRepository } from "../../../../catalogue/repository/catalogue.repository.js";
import "../../../../automation/workspace/automation/canvas/ua-automation-canvas.element.js";

const TRIGGER_NODE_ID = "__trigger__";

@customElement("ua-run-canvas-view")
export class UaRunCanvasViewElement extends UmbLitElement {
    #catalogueRepository: UaCatalogueRepository;

    @state()
    private _nodes: Node[] = [];

    @state()
    private _edges: Edge[] = [];

    @state()
    private _viewport?: { x: number; y: number; zoom: number };

    @state()
    private _run?: UaRunDetailModel;

    constructor() {
        super();
        this.#catalogueRepository = new UaCatalogueRepository(this);
        this.consumeContext(UA_RUN_WORKSPACE_CONTEXT, (context) => {
            if (!context) return;
            this.observe(context.run, (run) => {
                this._run = run;
                this.#rebuildCanvas();
            });
            this.observe(context.automation, (automation) => {
                this.#automation = automation;
                this.#rebuildCanvas();
            });
        });
    }

    #automation?: UaAutomationDetailModel;

    async #rebuildCanvas() {
        if (!this.#automation || !this._run) return;

        const canvasState = this.#parseCanvasState(this.#automation.canvasState);
        const catalogueNames = await this.#buildCatalogueNames();
        const nodes = modelToNodes(this.#automation.trigger, this.#automation.steps, canvasState, catalogueNames);
        const edges = modelToEdges(this.#automation.connections);

        // Apply step run status as CSS classes via node data
        this._nodes = nodes.map((node) => {
            if (node.id === TRIGGER_NODE_ID) {
                // Trigger status based on overall run status
                const triggerStatus = this._run!.status === "Pending" ? "Pending" : "Completed";
                return {
                    ...node,
                    className: `run-status-${triggerStatus.toLowerCase()}`,
                    data: { ...node.data, runStatus: triggerStatus },
                    draggable: false,
                    connectable: false,
                };
            }

            const stepRun = this._run!.stepRuns.find((sr) => sr.stepId === node.id);
            const status = stepRun?.status ?? "Pending";
            return {
                ...node,
                className: `run-status-${status.toLowerCase()}`,
                data: { ...node.data, runStatus: status, stepRun },
                draggable: false,
                connectable: false,
            };
        });

        this._edges = edges.map((edge) => ({
            ...edge,
            animated: false,
            selectable: false,
        }));

        this._viewport = canvasState?.viewport;
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

    override render() {
        if (!this._run || !this.#automation) {
            return html`<div class="center"><uui-loader></uui-loader></div>`;
        }

        return html`
            <ua-automation-canvas
                read-only
                .nodes=${this._nodes}
                .edges=${this._edges}
                .viewport=${this._viewport}
            ></ua-automation-canvas>
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

            .center {
                display: flex;
                justify-content: center;
                align-items: center;
                height: 100%;
            }
        `,
    ];
}

export default UaRunCanvasViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-run-canvas-view": UaRunCanvasViewElement;
    }
}
