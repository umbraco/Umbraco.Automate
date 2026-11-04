import type { Edge } from "@xyflow/react";
import type { StepConfigurationModel } from "../../api/types.gen.js";
import type { UaCatalogueRepository } from "../../catalogue/repository/catalogue.repository.js";
import type { UaStepOutcome } from "../../catalogue/types.js";
import type { CatalogueLookupEntry } from "../../automation/workspace/automation/canvas/types.js";
import { ANY_RESULT_HANDLE, isActionNodeType } from "../../automation/workspace/automation/canvas/utils/model-to-flow.js";
import type { UaStepRunModel } from "../types.js";

/** Class on an edge leaving a step through the exit the run took (or an "Any result" edge). */
const EDGE_TAKEN_CLASS = "ua-edge--taken";
/** Class on a named edge leaving a step through an exit the run did not take. */
const EDGE_NOT_TAKEN_CLASS = "ua-edge--not-taken";

/**
 * Resolves the outcomes of every plain action step whose action has dynamic outcomes, from the
 * step's saved settings (as the editor does). A step whose request fails is left out of the map,
 * so callers fall back to "unknown" outcomes rather than "no outcomes".
 */
export async function resolveDynamicStepOutcomes(
    repository: UaCatalogueRepository,
    steps: readonly StepConfigurationModel[],
    catalogue: ReadonlyMap<string, CatalogueLookupEntry>,
): Promise<Map<string, UaStepOutcome[]>> {
    const dynamicSteps = steps.filter(
        (s) => isActionNodeType(s.actionAlias) && catalogue.get(s.actionAlias)?.hasDynamicOutcomes === true,
    );
    const results = await Promise.all(
        dynamicSteps.map(async (step) => {
            const { data } = await repository.resolveOutcomes(step.actionAlias, step.settings);
            return data ? ([step.id, data] as const) : undefined;
        }),
    );
    return new Map(results.filter((entry) => entry !== undefined));
}

/**
 * Marks the edges leaving each branching step. A step can have several runs (inside a loop), so
 * all of its branch outcomes count: a named edge matching any of them gets the taken class, and
 * one matching none the not-taken class (also flagged in the edge data so its label dims too).
 * Unnamed ("Any result") edges from a step that branched get the taken class since they always
 * fire. Steps without any `branchOutcome` (non-branching, failed, old runs) are left untouched.
 */
export function applyBranchStyling(edges: Edge[], stepRuns: readonly UaStepRunModel[]): Edge[] {
    const branchOutcomesByStep = new Map<string, Set<string>>();
    for (const stepRun of stepRuns) {
        if (!stepRun.branchOutcome) continue;
        const outcomes = branchOutcomesByStep.get(stepRun.stepId) ?? new Set<string>();
        outcomes.add(stepRun.branchOutcome);
        branchOutcomesByStep.set(stepRun.stepId, outcomes);
    }

    return edges.map((edge) => {
        const branchOutcomes = branchOutcomesByStep.get(edge.source);
        if (!branchOutcomes) return edge;

        // If/Switch/Approval save their handle id as the outcome; actions save the outcome name.
        const edgeOutcome = typeof edge.label === "string" ? edge.label : edge.sourceHandle;
        const isNamed = !!edgeOutcome && edgeOutcome !== ANY_RESULT_HANDLE;
        const taken = !isNamed || branchOutcomes.has(edgeOutcome);
        return {
            ...edge,
            className: taken ? EDGE_TAKEN_CLASS : EDGE_NOT_TAKEN_CLASS,
            data: { ...edge.data, notTaken: !taken },
        };
    });
}
