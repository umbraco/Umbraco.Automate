import type { EditableModelSchemaModel } from "../api/types.gen.js";

/** A named exit a step can take, e.g. "Found" / "Not found". */
export interface UaStepOutcome {
    key: string;
    label: string;
    isDefault: boolean;
}

export interface UaCatalogueItemModel {
    alias: string;
    name: string;
    description: string | null;
    group: string | null;
    icon: string | null;
    settingsSchema: EditableModelSchemaModel | null;
    /**
     * Set by pickers when the item is listed but cannot be chosen in the current context
     * (e.g. the workspace has no allowed connection of the type the action needs).
     * A human-readable explanation shown alongside the disabled item.
     */
    unavailableReason?: string;
}

export interface UaTriggerCatalogueItemModel extends UaCatalogueItemModel {
    outputSchema: { [key: string]: unknown } | null;
    /** When true, the output schema depends on the step's settings and must be resolved via the catalogue resolve endpoint. */
    hasDynamicOutputSchema: boolean;
    /** The step type's declared outcomes. For dynamic step types this is only the static fallback. */
    outcomes: UaStepOutcome[];
    /** When true, the outcomes depend on the step's settings and must be resolved via the catalogue outcomes endpoint. */
    hasDynamicOutcomes: boolean;
    /** When true, an automation using this trigger can be started on demand ("Run now"). */
    supportsManualRun: boolean;
}

export interface UaActionCatalogueItemModel extends UaCatalogueItemModel {
    connectionTypeAlias: string | null;
    outputSchema: { [key: string]: unknown } | null;
    /** When true, the output schema depends on the step's settings and must be resolved via the catalogue resolve endpoint. */
    hasDynamicOutputSchema: boolean;
    /** The step type's declared outcomes. For dynamic step types this is only the static fallback. */
    outcomes: UaStepOutcome[];
    /** When true, the outcomes depend on the step's settings and must be resolved via the catalogue outcomes endpoint. */
    hasDynamicOutcomes: boolean;
}

export interface UaConnectionTypeCatalogueItemModel extends UaCatalogueItemModel {}

export interface UaControlFlowCatalogueItemModel extends UaCatalogueItemModel {
    outputSchema: { [key: string]: unknown } | null;
    /** When true, the output schema depends on the step's settings and must be resolved via the catalogue resolve endpoint. */
    hasDynamicOutputSchema: boolean;
    /** The step type's declared outcomes. For dynamic step types this is only the static fallback. */
    outcomes: UaStepOutcome[];
    /** When true, the outcomes depend on the step's settings and must be resolved via the catalogue outcomes endpoint. */
    hasDynamicOutcomes: boolean;
}

export type UaCatalogueMode = "trigger" | "action";
