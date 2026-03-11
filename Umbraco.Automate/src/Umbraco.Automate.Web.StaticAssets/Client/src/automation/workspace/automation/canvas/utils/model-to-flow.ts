import type { Node, Edge } from "@xyflow/react";
import type {
    TriggerConfigurationModel,
    StepConfigurationModel,
    StepConnectionModel,
} from "../../../../../api/types.gen.js";
import type { CanvasState, TriggerNodeData, ActionNodeData } from "../types.js";

const TRIGGER_NODE_ID = "__trigger__";
const DEFAULT_TRIGGER_POSITION = { x: 250, y: 50 };

export function modelToNodes(
    trigger: TriggerConfigurationModel | null,
    steps: StepConfigurationModel[],
    canvasState: CanvasState | null,
): Node[] {
    const nodes: Node[] = [];

    if (trigger) {
        const position = canvasState?.triggerPosition ?? DEFAULT_TRIGGER_POSITION;
        nodes.push({
            id: TRIGGER_NODE_ID,
            type: "trigger",
            position,
            data: {
                triggerAlias: trigger.triggerAlias,
                label: trigger.triggerAlias,
                settings: trigger.settings,
            } satisfies TriggerNodeData,
        });
    }

    for (const step of steps) {
        nodes.push({
            id: step.id,
            type: "action",
            position: { x: step.position.x, y: step.position.y },
            data: {
                stepId: step.id,
                actionAlias: step.actionAlias,
                label: step.name,
                settings: step.settings,
            } satisfies ActionNodeData,
        });
    }

    return nodes;
}

const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

export function modelToEdges(
    connections: StepConnectionModel[],
): Edge[] {
    return connections.map((conn, index) => ({
        id: `edge-${index}`,
        source: !conn.sourceStepId || conn.sourceStepId === EMPTY_GUID ? TRIGGER_NODE_ID : conn.sourceStepId,
        sourceHandle: conn.sourceHandle ?? undefined,
        target: conn.targetStepId,
        targetHandle: conn.targetHandle ?? undefined,
        type: "automation",
        label: conn.outcome ?? undefined,
    }));
}

export { TRIGGER_NODE_ID, DEFAULT_TRIGGER_POSITION };
