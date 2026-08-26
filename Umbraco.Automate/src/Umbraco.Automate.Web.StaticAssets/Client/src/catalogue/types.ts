import type { EditableModelSchemaModel } from "../api/types.gen.js";

export interface UaCatalogueItemModel {
    alias: string;
    name: string;
    description: string | null;
    group: string | null;
    icon: string | null;
    settingsSchema: EditableModelSchemaModel | null;
}

export interface UaTriggerCatalogueItemModel extends UaCatalogueItemModel {
    outputSchema: { [key: string]: unknown } | null;
    /** When true, the output schema depends on the step's settings and must be resolved via the catalogue resolve endpoint. */
    hasDynamicOutputSchema: boolean;
    /** When true, an automation using this trigger can be started on demand ("Run now"). */
    supportsManualRun: boolean;
}

export interface UaActionCatalogueItemModel extends UaCatalogueItemModel {
    connectionTypeAlias: string | null;
    outputSchema: { [key: string]: unknown } | null;
    /** When true, the output schema depends on the step's settings and must be resolved via the catalogue resolve endpoint. */
    hasDynamicOutputSchema: boolean;
}

export interface UaConnectionTypeCatalogueItemModel extends UaCatalogueItemModel {}

export interface UaControlFlowCatalogueItemModel extends UaCatalogueItemModel {
    outputSchema: { [key: string]: unknown } | null;
    /** When true, the output schema depends on the step's settings and must be resolved via the catalogue resolve endpoint. */
    hasDynamicOutputSchema: boolean;
}

export type UaCatalogueMode = "trigger" | "action";
