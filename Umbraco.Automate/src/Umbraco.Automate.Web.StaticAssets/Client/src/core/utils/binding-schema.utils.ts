import type { StepConnectionModel } from "../../api/types.gen.js";

export interface BindingLeaf {
    /** Dot-separated path relative to the source (e.g. "responseBody" or "response.headers") */
    path: string;
    /** Human-readable label */
    label: string;
    /** JSON Schema type (e.g. "string", "integer", "boolean") */
    type: string;
}

const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

/**
 * Recursively walks a JSON Schema `properties` object and produces flat leaf entries.
 * Nested objects are dot-joined (e.g. `response.statusCode`).
 */
export function flattenJsonSchema(
    schema: Record<string, unknown> | null | undefined,
    prefix = "",
): BindingLeaf[] {
    if (!schema) return [];

    const properties = schema.properties as Record<string, Record<string, unknown>> | undefined;
    if (!properties) return [];

    const leaves: BindingLeaf[] = [];

    for (const [key, prop] of Object.entries(properties)) {
        const fullPath = prefix ? `${prefix}.${key}` : key;
        const type = (prop.type as string) ?? "unknown";

        if (type === "object" && prop.properties) {
            // Recurse into nested objects
            leaves.push(...flattenJsonSchema(prop as Record<string, unknown>, fullPath));
        } else {
            leaves.push({ path: fullPath, label: key, type });
        }
    }

    return leaves;
}

/**
 * BFS backwards through connections to find all predecessor step IDs reachable
 * from the given step. Returns the set of predecessor step IDs and whether the
 * trigger is reachable.
 */
export function computePredecessors(
    stepId: string,
    connections: StepConnectionModel[],
): { predecessorIds: string[]; triggerReachable: boolean } {
    const visited = new Set<string>();
    let triggerReachable = false;
    const queue = [stepId];

    while (queue.length > 0) {
        const current = queue.shift()!;
        for (const conn of connections) {
            if (conn.targetStepId !== current) continue;

            const source = conn.sourceStepId;
            if (!source || source === EMPTY_GUID) {
                triggerReachable = true;
            } else if (!visited.has(source)) {
                visited.add(source);
                queue.push(source);
            }
        }
    }

    return { predecessorIds: Array.from(visited), triggerReachable };
}
