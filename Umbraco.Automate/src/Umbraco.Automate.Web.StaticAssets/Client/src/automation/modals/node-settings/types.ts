import type {
    EditableModelSchemaModel,
    StepConfigurationModel,
    StepConnectionModel,
    StepErrorBehaviorModel,
    TriggerConfigurationModel,
} from "../../../api/types.gen.js";

export interface UaNodeSettingsModalData {
    stepId: string;
    actionAlias: string;
    actionName: string;
    name: string;
    alias: string | null;
    /** True when the step was just added to the canvas and has not been saved yet — enables auto-generating the alias from the name. */
    isNew: boolean;
    settings: Record<string, unknown>;
    schema: EditableModelSchemaModel;
    /** Current explicit connection override for the step, or null to auto-resolve. */
    connectionId: string | null;
    /** Id of the workspace that owns the automation — scopes the available connections. */
    workspaceId: string;
    errorBehavior: StepErrorBehaviorModel;
    retryInterval: string | null;
    maxRetries: number | null;
    /** Automation context for computing binding sources. */
    automationContext?: {
        trigger: TriggerConfigurationModel | null;
        steps: StepConfigurationModel[];
        connections: StepConnectionModel[];
    };
}

export interface UaNodeSettingsModalValue {
    name: string;
    alias: string | null;
    settings: Record<string, unknown>;
    connectionId: string | null;
    errorBehavior: StepErrorBehaviorModel;
    retryInterval: string | null;
    maxRetries: number | null;
}
