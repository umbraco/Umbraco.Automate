import type { EditableModelSchemaModel, StepConfigurationModel, StepConnectionModel, TriggerConfigurationModel } from "../../../api/types.gen.js";

export interface UaNodeSettingsModalData {
    stepId: string;
    actionAlias: string;
    actionName: string;
    settings: Record<string, unknown>;
    schema: EditableModelSchemaModel;
    /** Automation context for computing binding sources. */
    automationContext?: {
        trigger: TriggerConfigurationModel | null;
        steps: StepConfigurationModel[];
        connections: StepConnectionModel[];
    };
}

export interface UaNodeSettingsModalValue {
    settings: Record<string, unknown>;
}
