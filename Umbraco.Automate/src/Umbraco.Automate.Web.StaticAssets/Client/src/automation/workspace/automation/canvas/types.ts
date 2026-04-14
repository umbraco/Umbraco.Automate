export interface TriggerNodeData {
    triggerAlias: string;
    label: string;
    settings: Record<string, unknown>;
    [key: string]: unknown;
}

export interface ActionNodeData {
    stepId: string;
    actionAlias: string;
    label: string;
    settings: Record<string, unknown>;
    cases?: string[];
    [key: string]: unknown;
}

export interface CanvasState {
    viewport: { x: number; y: number; zoom: number };
    triggerPosition?: { x: number; y: number };
}

export interface CanvasChangeDetail {
    nodes: import("@xyflow/react").Node[];
    edges: import("@xyflow/react").Edge[];
    viewport: { x: number; y: number; zoom: number };
}

export interface NodeSettingsOpenDetail {
    nodeId: string;
    nodeType: "trigger" | "action";
}

export interface AddNodeRequestDetail {
    position: { x: number; y: number };
}

export interface NodeDeleteRequestDetail {
    nodes: import("@xyflow/react").Node[];
    resolve: (confirmed: boolean) => void;
}

export type { ConditionSetModel, ConditionGroupModel, ConditionModel } from "../../../../api/types.gen.js";
import type { ConditionSetModel } from "../../../../api/types.gen.js";

export interface EdgeFilterData {
    filter?: ConditionSetModel | null;
}

export interface EdgeFilterOpenDetail {
    edgeId: string;
    filter: ConditionSetModel | null;
}
