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
}

export interface UaActionCatalogueItemModel extends UaCatalogueItemModel {
    connectionTypeAlias: string | null;
}

export interface UaConnectionTypeCatalogueItemModel extends UaCatalogueItemModel {}

export type UaCatalogueMode = "trigger" | "action";
