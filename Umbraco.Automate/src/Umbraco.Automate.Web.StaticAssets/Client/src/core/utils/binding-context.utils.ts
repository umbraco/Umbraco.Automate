import type { StepConfigurationModel, StepConnectionModel, TriggerConfigurationModel } from "../../api/types.gen.js";
import type { UaCatalogueRepository } from "../../catalogue/repository/catalogue.repository.js";
import { type BindingLeaf, computePredecessors, flattenJsonSchema } from "./binding-schema.utils.js";

const FOR_EACH_ALIAS = "umbracoAutomate.forEach";

export interface BindingSource {
    /** Unique identifier (e.g. "trigger" or step GUID) */
    id: string;
    /** Display name (e.g. "Webhook" or user-given step name) */
    label: string;
    /** Icon alias */
    icon: string;
    /** Binding prefix for expressions (e.g. "trigger" or "steps.abc-123") */
    bindingPrefix: string;
    /** Available output properties */
    leaves: BindingLeaf[];
}

/**
 * Builds the binding source tree for a given step in an automation.
 * Computes predecessors via the connections DAG, then resolves output schemas
 * from the catalogue for the trigger and each predecessor step.
 * If the step is inside a ForEach, adds a "Loop Item" source with the item schema.
 */
export async function buildBindingSources(
    currentStepId: string,
    trigger: TriggerConfigurationModel | null,
    steps: StepConfigurationModel[],
    connections: StepConnectionModel[],
    catalogueRepo: UaCatalogueRepository,
): Promise<BindingSource[]> {
    const sources: BindingSource[] = [];
    const { predecessorIds, triggerReachable } = computePredecessors(currentStepId, connections);

    // Trigger source — resolve schema and cache for loop item resolution below.
    let triggerOutputSchema: Record<string, unknown> | null = null;
    if (triggerReachable && trigger) {
        const { data: triggers } = await catalogueRepo.requestTriggers();
        const triggerItem = triggers?.find((t) => t.alias === trigger.triggerAlias);
        if (triggerItem?.outputSchema) {
            triggerOutputSchema = triggerItem.outputSchema as Record<string, unknown>;
            const leaves = flattenJsonSchema(triggerOutputSchema);
            if (leaves.length > 0) {
                sources.push({
                    id: "trigger",
                    label: triggerItem.name,
                    icon: triggerItem.icon ?? "icon-flash",
                    bindingPrefix: "trigger",
                    leaves,
                });
            }
        }
    }

    // Predecessor step sources
    const [actionsResult, controlFlowsResult] = await Promise.all([
        catalogueRepo.requestActions(),
        catalogueRepo.requestControlFlows(),
    ]);
    const actions = actionsResult.data ?? [];
    const controlFlows = controlFlowsResult.data ?? [];

    for (const predId of predecessorIds) {
        const step = steps.find((s) => s.id === predId);
        if (!step) continue;

        // Look up catalogue item for this step's action alias
        const actionItem = actions.find((a) => a.alias === step.actionAlias);
        const controlFlowItem = controlFlows.find((c) => c.alias === step.actionAlias);
        const catalogueItem = actionItem ?? controlFlowItem;
        if (!catalogueItem) continue;

        const outputSchema = (actionItem?.outputSchema ?? controlFlowItem?.outputSchema) as Record<string, unknown> | null;
        if (!outputSchema) continue;

        const leaves = flattenJsonSchema(outputSchema);
        if (leaves.length === 0) continue;

        sources.push({
            id: predId,
            label: step.name || catalogueItem.name,
            icon: catalogueItem.icon ?? "icon-settings",
            bindingPrefix: `steps.${predId}`,
            leaves,
        });
    }

    // Loop item source — if the step is inside a ForEach, resolve the item schema.
    const forEachStep = findNearestForEach(steps, predecessorIds);
    if (forEachStep) {
        const itemSchema = resolveLoopItemSchema(forEachStep, triggerOutputSchema, steps, actions, controlFlows);
        const loopLeaves: BindingLeaf[] = [{ path: "index", label: "index", type: "integer" }];

        if (itemSchema) {
            const itemLeaves = flattenJsonSchema(itemSchema);
            for (const leaf of itemLeaves) {
                loopLeaves.push({ ...leaf, path: `item.${leaf.path}`, label: leaf.label });
            }
        }

        sources.push({
            id: "loop",
            label: "Loop",
            icon: "icon-repeat",
            bindingPrefix: "loop",
            leaves: loopLeaves,
        });
    }

    return sources;
}

/**
 * Finds the nearest ForEach step among predecessors of the given step.
 */
function findNearestForEach(
    steps: StepConfigurationModel[],
    predecessorIds: string[],
): StepConfigurationModel | null {
    // Walk predecessors in order — the first ForEach found is the nearest parent.
    for (const predId of predecessorIds) {
        const step = steps.find((s) => s.id === predId);
        if (step?.actionAlias === FOR_EACH_ALIAS) {
            return step;
        }
    }
    return null;
}

/**
 * Resolves the JSON Schema of a single item inside a ForEach collection.
 * Parses the ForEach's Collection binding, finds the source array schema,
 * and extracts its `items` sub-schema.
 */
function resolveLoopItemSchema(
    forEachStep: StepConfigurationModel,
    triggerOutputSchema: Record<string, unknown> | null,
    steps: StepConfigurationModel[],
    actions: Array<{ alias: string; outputSchema?: Record<string, unknown> | null }>,
    controlFlows: Array<{ alias: string; outputSchema?: Record<string, unknown> | null }>,
): Record<string, unknown> | null {
    const collection = (forEachStep.settings as Record<string, unknown> | undefined)?.collection as string | undefined
        ?? (forEachStep.settings as Record<string, unknown> | undefined)?.Collection as string | undefined;
    if (!collection) return null;

    // Parse binding expression: ${ trigger.items } or ${ steps.abc.output }
    const match = collection.match(/\$\{\s*(\S+?)\s*\}/);
    if (!match) return null;

    const fullPath = match[1]; // e.g. "trigger.items" or "steps.abc-123.output"
    const segments = fullPath.split(".");

    let schema: Record<string, unknown> | null = null;
    let pathStart = 0;

    if (segments[0] === "trigger") {
        schema = triggerOutputSchema;
        pathStart = 1;
    } else if (segments[0] === "steps" && segments.length >= 3) {
        const stepId = segments[1];
        const step = steps.find((s) => s.id === stepId);
        if (step) {
            const actionItem = actions.find((a) => a.alias === step.actionAlias);
            const controlFlowItem = controlFlows.find((c) => c.alias === step.actionAlias);
            schema = (actionItem?.outputSchema ?? controlFlowItem?.outputSchema) as Record<string, unknown> | null;
        }
        pathStart = 2;
    }

    if (!schema) return null;

    // Navigate the schema to find the referenced property
    let current = schema;
    for (let i = pathStart; i < segments.length; i++) {
        const properties = current.properties as Record<string, Record<string, unknown>> | undefined;
        if (!properties) return null;

        current = properties[segments[i]];
        if (!current) return null;
    }

    // If it's an array, return the items schema (the individual item type)
    if (current.type === "array" && current.items) {
        return current.items as Record<string, unknown>;
    }

    return null;
}
