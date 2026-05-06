import { css, html, customElement, property, state } from "@umbraco-cms/backoffice/external/lit";
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
import { aggregateStatus, computeBranchState } from "../../../../automation/workspace/automation/canvas/nodes/run/run-node-utils.js";
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
    private _run?: UaRunDetailModel;

    @state()
    private _automation?: UaAutomationDetailModel;

    /**
     * Optional explicit run override — when set, takes precedence over the workspace context.
     * Lets the view be embedded outside the run workspace (e.g. inside a detail modal).
     */
    @property({ attribute: false })
    set run(value: UaRunDetailModel | undefined) {
        this._run = value;
        this.#rebuildCanvas();
    }
    get run() {
        return this._run;
    }

    /** Optional explicit automation override — see {@link run}. */
    @property({ attribute: false })
    set automation(value: UaAutomationDetailModel | undefined) {
        this._automation = value;
        this.#rebuildCanvas();
    }
    get automation() {
        return this._automation;
    }

    constructor() {
        super();
        this.#catalogueRepository = new UaCatalogueRepository(this);
        this.consumeContext(UA_RUN_WORKSPACE_CONTEXT, (context) => {
            if (!context) return;
            this.observe(context.run, (run) => {
                if (this._run) return; // explicit prop takes precedence
                this._run = run;
                this.#rebuildCanvas();
            });
            this.observe(context.automation, (automation) => {
                if (this._automation) return; // explicit prop takes precedence
                this._automation = automation;
                this.#rebuildCanvas();
            });
        });
    }

    async #rebuildCanvas() {
        if (!this._automation || !this._run) return;

        const canvasState = this.#parseCanvasState(this._automation.canvasState);
        const catalogue = await this.#buildCatalogueLookup();
        const baseNodes = modelToNodes(this._automation.trigger, this._automation.steps, canvasState, catalogue);
        const baseEdges = modelToEdges(this._automation.connections);

        // A step can have multiple stepRuns when it lives under a control-flow body
        // (forEach, while, parallel iterations) — group by stepId so the node can
        // surface iteration count and aggregate status/duration.
        const stepRunsByStepId = new Map<string, UaStepRunModel[]>();
        for (const sr of this._run.stepRuns) {
            const list = stepRunsByStepId.get(sr.stepId) ?? [];
            list.push(sr);
            stepRunsByStepId.set(sr.stepId, list);
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

            const stepRuns = stepRunsByStepId.get(node.id) ?? [];
            const status = aggregateStatus(stepRuns);
            const data: Record<string, unknown> = {
                ...node.data,
                runStatus: status,
                stepRuns,
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

            // Surface iteration metadata for control-flow container actions (forEach today;
            // while/parallel can join later by alias). bodyIterations comes from the runtime —
            // count of stepRuns on the first downstream body node, which equals the loop count.
            const actionAlias = (node.data as { actionAlias?: string }).actionAlias;
            if (actionAlias === "umbracoAutomate.forEach") {
                const settings = (node.data as { settings?: Record<string, unknown> }).settings ?? {};
                const collection = (settings.Collection ?? settings.collection) as string | undefined;
                if (collection) data.collectionBinding = collection;
                data.bodyIterations = countBodyIterations(node.id, baseEdges, stepRunsByStepId);
            }

            return {
                ...node,
                data,
                draggable: false,
                connectable: false,
            };
        });

        this._edges = baseEdges.map((edge) => {
            const sourceCompleted = edge.source === TRIGGER_NODE_ID
                ? (this._run!.status !== "Pending")
                : aggregateStatus(stepRunsByStepId.get(edge.source) ?? []) === "Completed";
            const targetStatus = aggregateStatus(stepRunsByStepId.get(edge.target) ?? []);
            const taken = sourceCompleted && targetStatus !== "Pending" && targetStatus !== "Skipped";
            return {
                ...edge,
                animated: false,
                selectable: false,
                data: { ...edge.data, taken },
            };
        });

        // Intentionally not propagating canvasState.viewport: a run is read-only and the
        // editor's last-saved pan/zoom is irrelevant. Leaving `viewport` undefined makes
        // the canvas fitView on mount so every node is visible.
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
        if (!this._run || !this._automation) {
            return html`<div class="center"><uui-loader></uui-loader></div>`;
        }

        return html`
            <ua-automation-canvas
                read-only
                .nodes=${this._nodes}
                .edges=${this._edges}
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

/**
 * Count how many times a control-flow container's body executed at runtime by reading
 * the stepRun count of the first downstream node. Inside a forEach the body runs once
 * per item in the collection, so this equals the realised iteration count (0 when the
 * collection was empty).
 */
function countBodyIterations(
    containerNodeId: string,
    edges: Array<{ source: string; target: string }>,
    stepRunsByStepId: Map<string, ReadonlyArray<UaStepRunModel>>,
): number {
    let max = 0;
    for (const edge of edges) {
        if (edge.source !== containerNodeId) continue;
        const count = stepRunsByStepId.get(edge.target)?.length ?? 0;
        if (count > max) max = count;
    }
    return max;
}

declare global {
    interface HTMLElementTagNameMap {
        "ua-run-canvas-view": UaRunCanvasViewElement;
    }
}
