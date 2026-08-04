import type { Node, Edge } from "@xyflow/react";
import type {
    TriggerConfigurationModel,
    StepConfigurationModel,
    StepConnectionModel,
} from "../../../../../api/types.gen.js";
import type { CanvasState, CatalogueLookupEntry, TriggerNodeData, ActionNodeData } from "../types.js";

const TRIGGER_NODE_ID = "__trigger__";
const DEFAULT_TRIGGER_POSITION = { x: 250, y: 50 };

const IF_ALIAS = "umbracoAutomate.if";
const SWITCH_ALIAS = "umbracoAutomate.switch";
const APPROVAL_ALIAS = "umbracoAutomate.requestApproval";

/**
 * Outcome names the Request Approval step returns, used as its source handle ids so a connection
 * drawn from a handle is saved with the matching outcome. Must stay in step with
 * RequestApprovalAction.ApprovedOutcome / RejectedOutcome on the server.
 */
export const APPROVED_OUTCOME = "approved";
export const REJECTED_OUTCOME = "rejected";

function getNodeType(actionAlias: string): string {
    if (actionAlias === IF_ALIAS) return "if";
    if (actionAlias === SWITCH_ALIAS) return "switch";
    if (actionAlias === APPROVAL_ALIAS) return "approval";
    return "action";
}

export function modelToNodes(
    trigger: TriggerConfigurationModel | null,
    steps: StepConfigurationModel[],
    canvasState: CanvasState | null,
    catalogue?: Map<string, CatalogueLookupEntry>,
): Node[] {
    const nodes: Node[] = [];

    if (trigger) {
        const position = canvasState?.triggerPosition ?? DEFAULT_TRIGGER_POSITION;
        const entry = catalogue?.get(trigger.triggerAlias);
        nodes.push({
            id: TRIGGER_NODE_ID,
            type: "trigger",
            position,
            data: {
                triggerAlias: trigger.triggerAlias,
                label: entry?.name ?? trigger.triggerAlias,
                icon: entry?.icon,
                hasSettings: entry?.hasSettings ?? true,
                settings: trigger.settings,
            } satisfies TriggerNodeData,
        });
    } else {
        // Empty-state placeholder so the user has something to click to add a trigger.
        nodes.push({
            id: TRIGGER_NODE_ID,
            type: "trigger-placeholder",
            position: canvasState?.triggerPosition ?? DEFAULT_TRIGGER_POSITION,
            data: {},
            draggable: false,
            deletable: false,
            connectable: false,
        });
    }

    for (const step of steps) {
        const nodeType = getNodeType(step.actionAlias);
        const entry = catalogue?.get(step.actionAlias);
        const label = step.name === step.actionAlias
            ? (entry?.name ?? step.name)
            : step.name;

        const data: ActionNodeData = {
            stepId: step.id,
            stepAlias: step.alias ?? "",
            actionAlias: step.actionAlias,
            label,
            icon: entry?.icon,
            hasSettings: entry?.hasSettings ?? true,
            settings: step.settings,
        };

        // For switch nodes, extract case names from settings so the node can render dynamic handles.
        // Settings keys are camelCase (EditableModelSchemaBuilder derives field keys via ToCamelCase),
        // so the field is "cases" — the nested case objects keep their PascalCase Name/Conditions.
        if (nodeType === "switch") {
            const cases = step.settings?.cases as Array<{ Name: string }> | undefined;
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
        data: {
            filter: (conn as Record<string, unknown>).filter ?? null,
        },
    }));
}

export { TRIGGER_NODE_ID, DEFAULT_TRIGGER_POSITION };
