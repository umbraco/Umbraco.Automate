import type { StepConfigurationModel, StepConnectionModel, TriggerConfigurationModel } from "../../api/types.gen.js";
import type { UaCatalogueRepository } from "../../catalogue/repository/catalogue.repository.js";
import { type BindingLeaf, computePredecessors, flattenJsonSchema } from "./binding-schema.utils.js";

const FOR_EACH_ALIAS = "umbracoAutomate.forEach";
const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

export interface BindingSource {
    /** Unique identifier (e.g. "trigger" or step GUID) */
    id: string;
    /** Display name (e.g. "Webhook" or user-given step name) */
    label: string;
    /**
     * Secondary description shown beneath the label, e.g. the action's catalogue
     * name or the step's binding alias — disambiguates two steps that share a label.
     */
    subLabel?: string;
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
 * from the catalogue for the trigger and each predecessor step. Step types with a
 * dynamic output schema (e.g. Run AI Agent) are resolved against their configured
 * settings via the catalogue resolve endpoint rather than the static listing.
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

    // Fetch the catalogue listings up front (cached in the repo).
    const [triggersResult, actionsResult, controlFlowsResult] = await Promise.all([
        catalogueRepo.requestTriggers(),
        catalogueRepo.requestActions(),
        catalogueRepo.requestControlFlows(),
    ]);
    const triggers = triggersResult.data ?? [];
    const actions = actionsResult.data ?? [];
    const controlFlows = controlFlowsResult.data ?? [];

    // Resolves a catalogue item's output schema. For step types with a dynamic output schema
    // (e.g. Run AI Agent, whose shape depends on the selected agent), the static listing carries
    // no usable schema, so we ask the server to resolve it from the step's settings. Static step
    // types just use the CLR-derived schema already present on the listing.
    const resolveSchema = async (
        catalogueItem: { alias: string; outputSchema: Record<string, unknown> | null; hasDynamicOutputSchema: boolean } | undefined,
        settings: Record<string, unknown> | undefined,
    ): Promise<Record<string, unknown> | null> => {
        if (!catalogueItem) return null;
        if (catalogueItem.hasDynamicOutputSchema) {
            const { data } = await catalogueRepo.resolveOutputSchema(catalogueItem.alias, settings ?? {});
            return data ?? null;
        }
        return catalogueItem.outputSchema ?? null;
    };

    // Trigger source — resolve schema and cache for loop item resolution below.
    let triggerOutputSchema: Record<string, unknown> | null = null;
    if (triggerReachable && trigger) {
        const triggerItem = triggers.find((t) => t.alias === trigger.triggerAlias);
        triggerOutputSchema = await resolveSchema(triggerItem, trigger.settings);
        if (triggerItem && triggerOutputSchema) {
            const leaves = flattenJsonSchema(triggerOutputSchema);
            if (leaves.length > 0) {
                sources.push({
                    id: "trigger",
                    label: triggerItem.name,
                    subLabel: trigger.triggerAlias,
                    icon: triggerItem.icon ?? "icon-flash",
                    bindingPrefix: "trigger",
                    leaves,
                });
            }
        }
    }

    // Predecessor step sources. Cache each resolved schema by step id so the "Previous" source
    // and loop-item resolution below can reuse it without re-resolving.
    const stepSchemas = new Map<string, Record<string, unknown> | null>();
    for (const predId of predecessorIds) {
        const step = steps.find((s) => s.id === predId);
        if (!step) continue;

        // Look up catalogue item for this step's action alias
        const actionItem = actions.find((a) => a.alias === step.actionAlias);
        const controlFlowItem = controlFlows.find((c) => c.alias === step.actionAlias);
        const catalogueItem = actionItem ?? controlFlowItem;
        if (!catalogueItem) continue;

        const outputSchema = await resolveSchema(catalogueItem, step.settings);
        stepSchemas.set(step.id, outputSchema);
        if (!outputSchema) continue;

        const leaves = flattenJsonSchema(outputSchema);
        if (leaves.length === 0) continue;

        const bindingAlias = step.alias || predId;
        sources.push({
            id: predId,
            label: step.name || catalogueItem.name,
            // Binding alias — disambiguates two steps that share a display name and
            // matches the alias chip shown on the canvas action node.
            subLabel: bindingAlias,
            icon: catalogueItem.icon ?? "icon-settings",
            bindingPrefix: `steps.${bindingAlias}`,
            leaves,
        });
    }

    // "Previous" source — only when the step has exactly 1 direct predecessor (unambiguous).
    const directPredecessors = connections
        .filter((c) => c.targetStepId === currentStepId)
        .map((c) => c.sourceStepId)
        .filter((id) => id && id !== EMPTY_GUID);

    if (directPredecessors.length === 1) {
        const predStep = steps.find((s) => s.id === directPredecessors[0]);
        if (predStep) {
            const predAction = actions.find((a) => a.alias === predStep.actionAlias);
            const predControlFlow = controlFlows.find((c) => c.alias === predStep.actionAlias);
            const predCatalogue = predAction ?? predControlFlow;
            // The single direct predecessor is always among predecessorIds, so its schema
            // (static or dynamically resolved) was already computed and cached above.
            const predOutputSchema = stepSchemas.get(predStep.id) ?? null;

            if (predOutputSchema && predCatalogue) {
                const prevLeaves = flattenJsonSchema(predOutputSchema);
                if (prevLeaves.length > 0) {
                    sources.push({
                        id: "previous",
                        label: `Previous (${predStep.name || predCatalogue.name})`,
                        icon: "icon-arrow-left",
                        bindingPrefix: "previous",
                        leaves: prevLeaves,
                    });
                }
            }
        }
    }

    // Loop item source — if the step is inside a ForEach, resolve the item schema.
    const forEachStep = findNearestForEach(steps, predecessorIds);
    if (forEachStep) {
        const itemSchema = resolveLoopItemSchema(forEachStep, triggerOutputSchema, steps, stepSchemas);

        // Always expose the bare `item` so primitive-typed arrays (e.g. string[])
        // and whole-object pass-through both work from the picker.
        const loopLeaves: BindingLeaf[] = [
            {
                path: "item",
                label: "item",
                type: (itemSchema?.type as string) ?? "unknown"
            },
            {
                path: "index",
                label: "index",
                type: "integer"
            }
        ];

        if (itemSchema) {
            for (const leaf of flattenJsonSchema(itemSchema)) {
                loopLeaves.push({
                    ...leaf,
                    path: `item.${leaf.path}`,
                    label: leaf.label
                });
            }
        }

        sources.push({
            id: "loop",
            label: "Loop",
            icon: "icon-repeat",
            bindingPrefix: "loop",
            leaves: loopLeaves
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
    stepSchemas: Map<string, Record<string, unknown> | null>,
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
        const stepRef = segments[1];
        const step = steps.find((s) => s.id === stepRef || s.alias === stepRef);
        // The ForEach's collection source must precede it, so its schema (static or dynamically
        // resolved) was already computed into stepSchemas during predecessor processing.
        if (step) {
            schema = stepSchemas.get(step.id) ?? null;
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
