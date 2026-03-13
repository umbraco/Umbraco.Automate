import type { EditableModelSchemaModel, TriggerOutputPropertyModel } from "../api/types.gen.js";

export interface UaCatalogueItemModel {
    alias: string;
    name: string;
    description: string | null;
    group: string | null;
    icon: string | null;
    settingsSchema: EditableModelSchemaModel | null;
}

export interface UaTriggerCatalogueItemModel extends UaCatalogueItemModel {
    outputProperties: TriggerOutputPropertyModel[];
}

export interface UaActionCatalogueItemModel extends UaCatalogueItemModel {}

export interface UaConnectionTypeCatalogueItemModel extends UaCatalogueItemModel {}

export type UaCatalogueMode = "trigger" | "action";
