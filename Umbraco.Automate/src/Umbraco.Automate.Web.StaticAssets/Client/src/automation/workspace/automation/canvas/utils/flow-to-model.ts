import type { Node, Edge } from "@xyflow/react";
import type {
    TriggerConfigurationModel,
    StepConfigurationModel,
    StepConnectionModel,
} from "../../../../../api/types.gen.js";
import type { CanvasState, TriggerNodeData, ActionNodeData } from "../types.js";
import { TRIGGER_NODE_ID } from "./model-to-flow.js";

export function flowToTrigger(
    nodes: Node[],
    currentTrigger: TriggerConfigurationModel | null,
): TriggerConfigurationModel | null {
    const triggerNode = nodes.find((n) => n.id === TRIGGER_NODE_ID);
    if (!triggerNode || !currentTrigger) return currentTrigger;

    const data = triggerNode.data as TriggerNodeData;
    return {
        triggerAlias: data.triggerAlias,
        settings: data.settings,
    };
}

export function flowToSteps(
    nodes: Node[],
    existingSteps: StepConfigurationModel[],
): StepConfigurationModel[] {
    return nodes
        .filter((n) => n.id !== TRIGGER_NODE_ID)
        .map((node) => {
            const data = node.data as ActionNodeData;
            const existing = existingSteps.find((s) => s.id === node.id);
            return {
                id: node.id,
                actionAlias: data.actionAlias,
                name: data.label,
                connectionId: existing?.connectionId ?? null,
                settings: data.settings,
                inputMappings: existing?.inputMappings ?? {},
                position: { x: node.position.x, y: node.position.y },
                errorBehavior: existing?.errorBehavior ?? "Terminate",
                retryInterval: existing?.retryInterval ?? null,
                maxRetries: existing?.maxRetries ?? null,
            };
        });
}

const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

export function flowToConnections(edges: Edge[]): StepConnectionModel[] {
    return edges.map((edge) => ({
        sourceStepId: edge.source === TRIGGER_NODE_ID ? EMPTY_GUID : edge.source,
        sourceHandle: edge.sourceHandle ?? null,
        targetStepId: edge.target,
        targetHandle: edge.targetHandle ?? null,
        outcome: (edge.label as string) ?? null,
    }));
}

export function flowToCanvasState(
    nodes: Node[],
    viewport: { x: number; y: number; zoom: number },
): CanvasState {
    const triggerNode = nodes.find((n) => n.id === TRIGGER_NODE_ID);
    return {
        viewport,
        triggerPosition: triggerNode
            ? { x: triggerNode.position.x, y: triggerNode.position.y }
            : undefined,
    };
}
