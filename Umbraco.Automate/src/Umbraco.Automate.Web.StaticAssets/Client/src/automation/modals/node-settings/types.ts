import type { EditableModelSchemaModel } from "../../../api/types.gen.js";

export interface UaNodeSettingsModalData {
    stepId: string;
    actionAlias: string;
    actionName: string;
    settings: Record<string, unknown>;
    schema: EditableModelSchemaModel;
}

export interface UaNodeSettingsModalValue {
    settings: Record<string, unknown>;
}
