import type { UaStepOutcome } from "../../../../catalogue/types.js";

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
    /**
     * True when the action declares outcomes (static or dynamic), so it renders exits on its
     * right edge. Set on action nodes only. Independent of how many exits there are right now.
     */
    declaresOutcomes?: boolean;
    /**
     * The exits to render, in declaration order. For a dynamic action this is the resolved list,
     * or the catalogue's static fallback when `outcomesUnknown` is true.
     */
    outcomes?: UaStepOutcome[];
    /**
     * True when a dynamic action's outcomes could not be resolved (request failed or returned
     * nothing). `outcomes` is then only the static fallback. T14 must NOT show a line as stale
     * ("Missing outcome") on such a node: render a neutral handle for every connected outcome key
     * and keep every line, without judging it.
     */
    outcomesUnknown?: boolean;
    [key: string]: unknown;
}

export interface CatalogueLookupEntry {
    name: string;
    icon?: string;
    hasSettings?: boolean;
    /** The action's declared outcomes (the static fallback when `hasDynamicOutcomes`). */
    outcomes?: UaStepOutcome[];
    /** True when the action's outcomes depend on its settings and must be resolved per step. */
    hasDynamicOutcomes?: boolean;
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
