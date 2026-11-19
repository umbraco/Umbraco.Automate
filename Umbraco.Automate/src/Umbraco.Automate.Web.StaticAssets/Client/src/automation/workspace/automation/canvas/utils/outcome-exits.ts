import type { UaStepOutcome } from "../../../../../catalogue/types.js";
import { ANY_RESULT_HANDLE } from "./model-to-flow.js";

export type OutcomeExitKind = "declared" | "any" | "missing" | "neutral";

export interface OutcomeExit {
    /** Handle id: the outcome key, or ANY_RESULT_HANDLE. */
    handleId: string;
    kind: OutcomeExitKind;
    /** Raw label (may be a `#term`); for "missing" this is the key, the node adds the prefix. */
    label: string;
    isDefault: boolean;
    /** Raw tooltip (may be a `#term`), if any. */
    tooltip?: string;
}

export interface OutcomeExits {
    exits: OutcomeExit[];
    /** True while an "Any result" line and at least one named line both leave the node. */
    hasBothPathsConflict: boolean;
}

/** Handle ids of the lines leaving a node, split into the unnamed and the named ones. */
export interface ConnectedExits {
    hasUnnamed: boolean;
    namedKeys: string[];
}

export function parseConnectedExits(handleIds: readonly (string | null | undefined)[]): ConnectedExits {
    const namedKeys: string[] = [];
    let hasUnnamed = false;
    for (const handleId of handleIds) {
        if (!handleId || handleId === ANY_RESULT_HANDLE) hasUnnamed = true;
        else if (!namedKeys.includes(handleId)) namedKeys.push(handleId);
    }
    return { hasUnnamed, namedKeys };
}

export function buildOutcomeExits(
    outcomes: readonly UaStepOutcome[],
    outcomesUnknown: boolean,
    connected: ConnectedExits,
): OutcomeExits {
    const exits: OutcomeExit[] = outcomes.map((o) => ({
        handleId: o.key,
        kind: "declared",
        label: o.label,
        isDefault: o.isDefault,
        tooltip: o.description,
    }));

    if (connected.hasUnnamed) {
        exits.push({
            handleId: ANY_RESULT_HANDLE,
            kind: "any",
            label: "#uaOutcomeExits_anyResult",
            isDefault: false,
            tooltip: "#uaOutcomeExits_anyResultTooltip",
        });
    }

    const declaredKeys = new Set(outcomes.map((o) => o.key));
    for (const key of connected.namedKeys) {
        if (declaredKeys.has(key)) continue;
        exits.push({
            handleId: key,
            kind: outcomesUnknown ? "neutral" : "missing",
            label: key,
            isDefault: false,
            tooltip: outcomesUnknown ? undefined : "#uaOutcomeExits_missingTooltip",
        });
    }

    return {
        exits,
        hasBothPathsConflict: connected.hasUnnamed && connected.namedKeys.length > 0,
    };
}
