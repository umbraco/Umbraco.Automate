import type { Node, Edge } from "@xyflow/react";
import type {
    TriggerConfigurationModel,
    StepConfigurationModel,
    StepConnectionModel,
} from "../../../../../api/types.gen.js";
import type { UaStepOutcome } from "../../../../../catalogue/types.js";
import type { CanvasState, CatalogueLookupEntry, TriggerNodeData, ActionNodeData } from "../types.js";

const TRIGGER_NODE_ID = "__trigger__";
const DEFAULT_TRIGGER_POSITION = { x: 250, y: 50 };

const IF_ALIAS = "umbracoAutomate.if";
const SWITCH_ALIAS = "umbracoAutomate.switch";
const APPROVAL_ALIAS = "umbracoAutomate.requestApproval";
export const PARALLEL_ALIAS = "umbracoAutomate.parallel";

/**
 * Control flow steps that own a body of steps. They render as ContainerNode, which carries a
 * body handle and a done handle rather than a single unnamed output.
 */
const CONTAINER_ALIASES = new Set([
    "umbracoAutomate.while",
    "umbracoAutomate.forEach",
    PARALLEL_ALIAS,
]);

/**
 * Outcome names the Request Approval step returns, used as its source handle ids so a connection
 * drawn from a handle is saved with the matching outcome. Must stay in step with
 * RequestApprovalAction.ApprovedOutcome / RejectedOutcome on the server.
 */
export const APPROVED_OUTCOME = "approved";
export const REJECTED_OUTCOME = "rejected";

/**
 * Source handle ids for container steps. A connection drawn from the body handle runs inside the
 * container; one drawn from the done handle runs once after it finishes. Must stay in step with
 * ContainerHandles on the server.
 */
export const BODY_HANDLE = "body";
export const DONE_HANDLE = "done";

/**
 * Outcome names the If and Switch steps return, used as their source handle ids. Must stay in
 * step with IfStepBody / SwitchStepBody on the server.
 */
export const IF_TRUE_OUTCOME = "true";
export const SWITCH_DEFAULT_OUTCOME = "default";

/**
 * Source handle id of the "Any result" exit: where an unnamed connection (no handle, no outcome)
 * leaving a step that declares outcomes is drawn. It is a display-only id: flow-to-model maps it
 * back to a null handle and null outcome, so saved data is unchanged.
 */
export const ANY_RESULT_HANDLE = "__any__";

/** The entry fields that decide whether an action declares outcomes. Mirrors the server. */
type OutcomeDeclaration = Pick<CatalogueLookupEntry, "outcomes" | "hasDynamicOutcomes">;

/** An action declares outcomes when they are dynamic or it lists at least one static outcome. */
export function declaresOutcomes(entry: OutcomeDeclaration | undefined): boolean {
    return !!entry && (entry.hasDynamicOutcomes === true || (entry.outcomes?.length ?? 0) > 0);
}

/** Whether a step with this action alias is rendered as a plain action node. */
export function isActionNodeType(actionAlias: string): boolean {
    return getNodeType(actionAlias) === "action";
}

function getNodeType(actionAlias: string): string {
    if (actionAlias === IF_ALIAS) return "if";
    if (actionAlias === SWITCH_ALIAS) return "switch";
    if (actionAlias === APPROVAL_ALIAS) return "approval";
    if (CONTAINER_ALIASES.has(actionAlias)) return "container";
    return "action";
}

// Settings keys are camelCase (EditableModelSchemaBuilder derives field keys via ToCamelCase),
// so the field is "cases" — the nested case objects keep their PascalCase Name/Conditions.
function getSwitchCaseNames(settings: StepConfigurationModel["settings"] | undefined): string[] {
    const cases = settings?.cases as Array<{ Name: string }> | undefined;
    return cases?.map((c) => c.Name) ?? [];
}

/**
 * The source handle a step continues through when it is spliced into an existing connection:
 * the done handle for containers (so the downstream step still runs after the container, not
 * inside it), the first branch for If/Switch/Request Approval, the default outcome (else the
 * first outcome) for an action that declares outcomes, and null for plain actions that only have
 * a single unnamed output. A declaring action whose outcomes are currently empty also gets null.
 *
 * `outcomes` is the step's current exits (resolved, for a dynamic action); pass the catalogue's
 * static list when nothing better is known.
 */
export function getContinuationSourceHandle(
    actionAlias: string,
    settings?: StepConfigurationModel["settings"],
    outcomes?: readonly UaStepOutcome[],
): string | null {
    switch (getNodeType(actionAlias)) {
        case "container":
            return DONE_HANDLE;
        case "if":
            return IF_TRUE_OUTCOME;
        case "switch":
            return getSwitchCaseNames(settings)[0] ?? SWITCH_DEFAULT_OUTCOME;
        case "approval":
            return APPROVED_OUTCOME;
        default:
            return (outcomes?.find((o) => o.isDefault) ?? outcomes?.[0])?.key ?? null;
    }
}

export function modelToNodes(
    trigger: TriggerConfigurationModel | null,
    steps: StepConfigurationModel[],
    canvasState: CanvasState | null,
    catalogue?: Map<string, CatalogueLookupEntry>,
    /**
     * Resolved outcomes per step id, for steps whose action has dynamic outcomes. A dynamic step
     * with no entry here is treated as "unknown" (see ActionNodeData.outcomesUnknown).
     */
    resolvedOutcomes?: ReadonlyMap<string, readonly UaStepOutcome[]>,
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

        // Only plain actions use the generic outcome exits; If/Switch/Approval/containers keep
        // their own handles for now.
        if (nodeType === "action" && declaresOutcomes(entry)) {
            data.declaresOutcomes = true;
            const resolved = entry?.hasDynamicOutcomes ? resolvedOutcomes?.get(step.id) : undefined;
            data.outcomes = [...(resolved ?? entry?.outcomes ?? [])];
            if (entry?.hasDynamicOutcomes && !resolved) {
                data.outcomesUnknown = true;
            }
        }

        // For switch nodes, extract case names from settings so the node can render dynamic handles.
        if (nodeType === "switch") {
            data.cases = getSwitchCaseNames(step.settings);
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

function isUnnamedFromDeclaringStep(
    conn: StepConnectionModel,
    outcomeDeclaringStepIds: ReadonlySet<string> | undefined,
): boolean {
    return (
        !!outcomeDeclaringStepIds?.has(conn.sourceStepId) && conn.sourceHandle == null && conn.outcome == null
    );
}

/** Ids of the nodes that declare outcomes, for deciding which unnamed lines show as "Any result". */
export function getOutcomeDeclaringNodeIds(nodes: readonly Node[]): Set<string> {
    return new Set(
        nodes.filter((n) => (n.data as ActionNodeData | undefined)?.declaresOutcomes === true).map((n) => n.id),
    );
}

export function modelToEdges(
    connections: StepConnectionModel[],
    outcomeDeclaringStepIds?: ReadonlySet<string>,
): Edge[] {
    return connections.map((conn, index) => ({
        id: `edge-${index}`,
        source: !conn.sourceStepId || conn.sourceStepId === EMPTY_GUID ? TRIGGER_NODE_ID : conn.sourceStepId,
        sourceHandle: isUnnamedFromDeclaringStep(conn, outcomeDeclaringStepIds)
            ? ANY_RESULT_HANDLE
            : (conn.sourceHandle ?? undefined),
        target: conn.targetStepId,
        targetHandle: conn.targetHandle ?? undefined,
        type: "automation",
        label: conn.outcome ?? undefined,
        data: {
            filter: (conn as Record<string, unknown>).filter ?? null,
        },
    }));
}

/**
 * Steps reachable from the trigger by following connections. Mirrors WorkflowCompiler.
 * TopologicalSort's own reachability pass on the server — steps not in this set are silently
 * dropped from the compiled workflow and never run.
 */
export function computeReachableFromTrigger(connections: StepConnectionModel[]): Set<string> {
    // Adjacency map built once so each connection is visited a single time, rather than
    // rescanning the full connections array per dequeued step.
    const adjacency = new Map<string, string[]>();
    for (const conn of connections) {
        const source = !conn.sourceStepId || conn.sourceStepId === EMPTY_GUID ? TRIGGER_NODE_ID : conn.sourceStepId;
        const targets = adjacency.get(source);
        if (targets) {
            targets.push(conn.targetStepId);
        } else {
            adjacency.set(source, [conn.targetStepId]);
        }
    }

    const reachable = new Set<string>();
    // Index-based traversal instead of queue.shift(), which is O(n) per call.
    const queue = [TRIGGER_NODE_ID];
    for (let i = 0; i < queue.length; i++) {
        const targets = adjacency.get(queue[i]);
        if (!targets) continue;
        for (const target of targets) {
            if (!reachable.has(target)) {
                reachable.add(target);
                queue.push(target);
            }
        }
    }
    return reachable;
}

export { TRIGGER_NODE_ID, DEFAULT_TRIGGER_POSITION };
