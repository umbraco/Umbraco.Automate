import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { Node, Edge } from "@xyflow/react";
import { UA_RUN_WORKSPACE_CONTEXT } from "../run-workspace.context-token.js";
import type { UaRunDetailModel, UaStepRunModel } from "../../../types.js";
import type { UaAutomationDetailModel } from "../../../../automation/types.js";
import { modelToNodes, modelToEdges } from "../../../../automation/workspace/automation/canvas/utils/model-to-flow.js";
import type { CanvasState, CatalogueLookupEntry } from "../../../../automation/workspace/automation/canvas/types.js";
import { runNodeTypes } from "../../../../automation/workspace/automation/canvas/nodes/run/run-node-types.js";
import RunEdge from "../../../../automation/workspace/automation/canvas/edges/RunEdge.js";
import { computeBranchState } from "../../../../automation/workspace/automation/canvas/nodes/run/run-node-utils.js";
import { UaCatalogueRepository } from "../../../../catalogue/repository/catalogue.repository.js";
import "../../../../automation/workspace/automation/canvas/ua-automation-canvas.element.js";
import runCanvasCss from "../../../../automation/workspace/automation/canvas/run-canvas.styles.css?inline";

const TRIGGER_NODE_ID = "__trigger__";

const runEdgeTypes = {
    automation: RunEdge,
};

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
        const catalogue = await this.#buildCatalogueLookup();
        const baseNodes = modelToNodes(this.#automation.trigger, this.#automation.steps, canvasState, catalogue);
        const baseEdges = modelToEdges(this.#automation.connections);

        const stepRunsByStepId = new Map<string, UaStepRunModel>();
        for (const sr of this._run.stepRuns) {
            stepRunsByStepId.set(sr.stepId, sr);
        }

        this._nodes = baseNodes.map((node) => {
            if (node.id === TRIGGER_NODE_ID) {
                const triggerStatus = this._run!.status === "Pending" ? "Pending" : "Completed";
                return {
                    ...node,
                    data: {
                        ...node.data,
                        runStatus: triggerStatus,
                        startedUtc: this._run!.startedUtc,
                    },
                    draggable: false,
                    connectable: false,
                };
            }

            const stepRun = stepRunsByStepId.get(node.id);
            const status = stepRun?.status ?? "Pending";
            const data: Record<string, unknown> = {
                ...node.data,
                runStatus: status,
                stepRun,
            };

            if (node.type === "if") {
                data.branches = {
                    true: computeBranchState(node.id, "true", baseEdges, stepRunsByStepId),
                    false: computeBranchState(node.id, "false", baseEdges, stepRunsByStepId),
                };
            } else if (node.type === "switch") {
                const branches: Record<string, ReturnType<typeof computeBranchState>> = {};
                const cases = (node.data as { cases?: string[] }).cases ?? [];
                for (const c of [...cases, "default"]) {
                    branches[c] = computeBranchState(node.id, c, baseEdges, stepRunsByStepId);
                }
                data.branches = branches;
            }

            return {
                ...node,
                data,
                draggable: false,
                connectable: false,
            };
        });

        this._edges = baseEdges.map((edge) => {
            const sourceRun = edge.source === TRIGGER_NODE_ID
                ? (this._run!.status !== "Pending")
                : stepRunsByStepId.get(edge.source)?.status === "Completed";
            const targetRun = stepRunsByStepId.get(edge.target);
            const taken = !!sourceRun && !!targetRun && targetRun.status !== "Pending" && targetRun.status !== "Skipped";
            return {
                ...edge,
                animated: false,
                selectable: false,
                data: { ...edge.data, taken },
            };
        });

        this._viewport = canvasState?.viewport;
    }

    async #buildCatalogueLookup(): Promise<Map<string, CatalogueLookupEntry>> {
        const lookup = new Map<string, CatalogueLookupEntry>();
        const [triggers, actions] = await Promise.all([
            this.#catalogueRepository.requestTriggers(),
            this.#catalogueRepository.requestActions(),
        ]);
        for (const t of triggers.data ?? []) {
            lookup.set(t.alias, { name: t.name, icon: t.icon ?? undefined });
        }
        for (const a of actions.data ?? []) {
            lookup.set(a.alias, { name: a.name, icon: a.icon ?? undefined });
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
                .nodeTypes=${runNodeTypes}
                .edgeTypes=${runEdgeTypes}
                .extraStyles=${runCanvasCss}
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
