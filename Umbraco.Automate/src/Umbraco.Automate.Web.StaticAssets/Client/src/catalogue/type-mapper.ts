import type { ActionItemResponseModel, ConnectionTypeItemResponseModel, TriggerItemResponseModel } from "../api/types.gen.js";
import type { UaActionCatalogueItemModel, UaConnectionTypeCatalogueItemModel, UaTriggerCatalogueItemModel } from "./types.js";

export const UaCatalogueTypeMapper = {
    toActionModel(response: ActionItemResponseModel): UaActionCatalogueItemModel {
        return {
            alias: response.alias,
            name: response.name,
            description: response.description ?? null,
            group: response.group ?? null,
            icon: response.icon ?? null,
            settingsSchema: response.settingsSchema ?? null,
            connectionTypeAlias: response.connectionTypeAlias ?? null,
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
            outputSchema: response.outputSchema ?? null,
        };
    },

    toConnectionTypeModel(response: ConnectionTypeItemResponseModel): UaConnectionTypeCatalogueItemModel {
        return {
            alias: response.alias,
            name: response.name,
            description: response.description ?? null,
            group: response.group ?? null,
            icon: response.icon ?? null,
            settingsSchema: response.settingsSchema ?? null,
        };
    },
};
