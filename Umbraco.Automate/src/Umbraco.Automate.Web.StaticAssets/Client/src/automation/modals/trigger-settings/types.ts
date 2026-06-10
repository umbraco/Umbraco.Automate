import type { EditableModelSchemaModel } from "../../../api/types.gen.js";

export interface UaTriggerSettingsModalData {
    triggerAlias: string;
    triggerName: string;
    settings: Record<string, unknown>;
    schema: EditableModelSchemaModel;
}

export interface UaTriggerSettingsModalValue {
    settings: Record<string, unknown>;
}
