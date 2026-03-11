import type { EditableModelSchemaResponseModel } from "../../../api/types.gen.js";

export interface UaTriggerSettingsModalData {
    triggerAlias: string;
    settings: Record<string, unknown>;
    schema: EditableModelSchemaResponseModel;
}

export interface UaTriggerSettingsModalValue {
    settings: Record<string, unknown>;
}
