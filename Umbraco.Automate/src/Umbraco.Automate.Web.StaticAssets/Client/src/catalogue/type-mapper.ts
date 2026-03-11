import type { ActionItemResponseModel, TriggerItemResponseModel } from "../api/types.gen.js";
import type { UaActionCatalogueItemModel, UaTriggerCatalogueItemModel } from "./types.js";

export const UaCatalogueTypeMapper = {
    toActionModel(response: ActionItemResponseModel): UaActionCatalogueItemModel {
        return {
            alias: response.alias,
            name: response.name,
            description: response.description ?? null,
            group: response.group ?? null,
            icon: response.icon ?? null,
            settingsSchema: response.settingsSchema ?? null,
        };
    },

    toTriggerModel(response: TriggerItemResponseModel): UaTriggerCatalogueItemModel {
        return {
            alias: response.alias,
            name: response.name,
            description: response.description ?? null,
            group: response.group ?? null,
            icon: response.icon ?? null,
            settingsSchema: response.settingsSchema ?? null,
            outputProperties: response.outputProperties,
        };
    },
};
