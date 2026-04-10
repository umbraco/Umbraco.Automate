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

export interface EdgeFilterData {
    filter?: ConditionSetModel | null;
}

export interface ConditionSetModel {
    groups: ConditionGroupModel[];
}

export interface ConditionGroupModel {
    conditions: ConditionModel[];
}

export interface ConditionModel {
    leftOperand: string;
    operator: string;
    rightOperand: string;
}

export interface EdgeFilterOpenDetail {
    edgeId: string;
    filter: ConditionSetModel | null;
}
