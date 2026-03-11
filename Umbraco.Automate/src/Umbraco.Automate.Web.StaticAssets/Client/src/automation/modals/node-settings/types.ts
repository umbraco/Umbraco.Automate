import type { EditableModelSchemaResponseModel } from "../../../api/types.gen.js";

export interface UaNodeSettingsModalData {
    stepId: string;
    actionAlias: string;
    settings: Record<string, unknown>;
    schema: EditableModelSchemaResponseModel;
}

export interface UaNodeSettingsModalValue {
    settings: Record<string, unknown>;
}
