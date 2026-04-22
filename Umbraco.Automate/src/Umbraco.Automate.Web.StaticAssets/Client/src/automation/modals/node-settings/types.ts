import type { EditableModelSchemaModel, StepConfigurationModel, StepConnectionModel, TriggerConfigurationModel } from "../../../api/types.gen.js";

export interface UaNodeSettingsModalData {
    stepId: string;
    actionAlias: string;
    actionName: string;
    settings: Record<string, unknown>;
    schema: EditableModelSchemaModel;
    /** Current explicit connection override for the step, or null to auto-resolve. */
    connectionId: string | null;
    /** Id of the workspace that owns the automation — scopes the available connections. */
    workspaceId: string;
    /** Automation context for computing binding sources. */
    automationContext?: {
        trigger: TriggerConfigurationModel | null;
        steps: StepConfigurationModel[];
        connections: StepConnectionModel[];
    };
}

export interface UaNodeSettingsModalValue {
    settings: Record<string, unknown>;
    connectionId: string | null;
}
