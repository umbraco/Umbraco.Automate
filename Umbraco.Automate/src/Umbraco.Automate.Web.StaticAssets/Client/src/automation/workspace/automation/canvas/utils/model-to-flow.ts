import type { Node, Edge } from "@xyflow/react";
import type {
    TriggerConfigurationModel,
    StepConfigurationModel,
    StepConnectionModel,
} from "../../../../../api/types.gen.js";
import type { CanvasState, TriggerNodeData, ActionNodeData } from "../types.js";

const TRIGGER_NODE_ID = "__trigger__";
const DEFAULT_TRIGGER_POSITION = { x: 250, y: 50 };

const IF_ALIAS = "umbracoAutomate.if";
const SWITCH_ALIAS = "umbracoAutomate.switch";

function getNodeType(actionAlias: string): string {
    if (actionAlias === IF_ALIAS) return "if";
    if (actionAlias === SWITCH_ALIAS) return "switch";
    return "action";
}

export function modelToNodes(
    trigger: TriggerConfigurationModel | null,
    steps: StepConfigurationModel[],
    canvasState: CanvasState | null,
    catalogueNames?: Map<string, string>,
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
                label: catalogueNames?.get(trigger.triggerAlias) ?? trigger.triggerAlias,
                settings: trigger.settings,
            } satisfies TriggerNodeData,
        });
    }

    for (const step of steps) {
        const nodeType = getNodeType(step.actionAlias);
        const label = step.name === step.actionAlias
            ? (catalogueNames?.get(step.actionAlias) ?? step.name)
            : step.name;

        const data: ActionNodeData = {
            stepId: step.id,
            actionAlias: step.actionAlias,
            label,
            settings: step.settings,
        };

        // For switch nodes, extract case names from settings so the node can render dynamic handles
        if (nodeType === "switch") {
            const cases = step.settings?.Cases as Array<{ Name: string }> | undefined;
            data.cases = cases?.map((c) => c.Name) ?? [];
        }

        nodes.push({
            id: step.id,
            type: nodeType,
            position: { x: step.position.x, y: step.position.y },
            data,
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
