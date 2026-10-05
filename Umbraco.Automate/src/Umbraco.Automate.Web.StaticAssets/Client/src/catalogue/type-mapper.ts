import type { StepOutcomeResponseModel, ActionItemResponseModel, ConnectionTypeItemResponseModel, ControlFlowItemResponseModel, TriggerItemResponseModel } from "../api/types.gen.js";
import type { UaActionCatalogueItemModel, UaStepOutcome, UaConnectionTypeCatalogueItemModel, UaControlFlowCatalogueItemModel, UaTriggerCatalogueItemModel } from "./types.js";

function toOutcome(response: StepOutcomeResponseModel): UaStepOutcome {
    return { key: response.key, label: response.label, isDefault: response.isDefault };
}

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
            outputSchema: response.outputSchema ?? null,
            hasDynamicOutputSchema: response.hasDynamicOutputSchema,
            outcomes: response.outcomes.map(toOutcome),
            hasDynamicOutcomes: response.hasDynamicOutcomes,
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
            hasDynamicOutputSchema: response.hasDynamicOutputSchema,
            outcomes: response.outcomes.map(toOutcome),
            hasDynamicOutcomes: response.hasDynamicOutcomes,
            supportsManualRun: response.supportsManualRun,
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

    toOutcomeModel: toOutcome,

    toControlFlowModel(response: ControlFlowItemResponseModel): UaControlFlowCatalogueItemModel {
        return {
            alias: response.alias,
            name: response.name,
            description: response.description ?? null,
            group: response.group ?? null,
            icon: response.icon ?? null,
            settingsSchema: response.settingsSchema ?? null,
            outputSchema: response.outputSchema ?? null,
            hasDynamicOutputSchema: response.hasDynamicOutputSchema,
            outcomes: response.outcomes.map(toOutcome),
            hasDynamicOutcomes: response.hasDynamicOutcomes,
        };
    },
};
