import type { StepConfigurationModel, StepConnectionModel, TriggerConfigurationModel } from "../../api/types.gen.js";
import type { UaCatalogueRepository } from "../../catalogue/repository/catalogue.repository.js";
import { type BindingLeaf, computePredecessors, flattenJsonSchema } from "./binding-schema.utils.js";

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

    // Trigger source
    if (triggerReachable && trigger) {
        const { data: triggers } = await catalogueRepo.requestTriggers();
        const triggerItem = triggers?.find((t) => t.alias === trigger.triggerAlias);
        if (triggerItem?.outputSchema) {
            const leaves = flattenJsonSchema(triggerItem.outputSchema as Record<string, unknown>);
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
    if (predecessorIds.length > 0) {
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
    }

    return sources;
}
