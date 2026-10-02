export interface TriggerNodeData {
    triggerAlias: string;
    label: string;
    icon?: string;
    hasSettings?: boolean;
    settings: Record<string, unknown>;
    [key: string]: unknown;
}

export interface ActionNodeData {
    stepId: string;
    stepAlias: string;
    actionAlias: string;
    label: string;
    icon?: string;
    hasSettings?: boolean;
    settings: Record<string, unknown>;
    cases?: string[];
    [key: string]: unknown;
}

export interface CatalogueLookupEntry {
    name: string;
    icon?: string;
    hasSettings?: boolean;
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
    /**
     * True when `position` was computed by the canvas (the node "+" button) rather than chosen by
     * the user (a drag dropped on the pane), so the view may move the node to a better slot.
     */
    autoPosition?: boolean;
    /**
     * When set, auto-connect the new node to this source. If that output already has a
     * connection (other than a Parallel body branch), the new node is spliced in front of the
     * existing target instead, as with `insertBetween`.
     */
    connectFrom?: {
        sourceStepId: string;
        sourceHandle?: string | null;
    };
    /**
     * When set, insert the new node onto an existing edge — splice it between
     * the source and target so the original A→B becomes A→new→B.
     */
    insertBetween?: {
        sourceStepId: string;
        sourceHandle?: string | null;
        targetStepId: string;
        targetHandle?: string | null;
    };
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
    source: string;
    sourceHandle: string | null;
    target: string;
    targetHandle: string | null;
    filter: ConditionSetModel | null;
}
