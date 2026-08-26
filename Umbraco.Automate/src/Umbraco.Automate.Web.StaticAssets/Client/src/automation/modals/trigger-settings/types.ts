import type { EditableModelSchemaModel } from "../../../api/types.gen.js";

export interface UaTriggerSettingsModalData {
    /**
     * Unique of the automation being edited. Trigger-specific panels that address the automation
     * itself use this — the webhook trigger's endpoint URL, for instance.
     */
    automationId: string;
    triggerAlias: string;
    triggerName: string;
    settings: Record<string, unknown>;
    schema: EditableModelSchemaModel;
}

export interface UaTriggerSettingsModalValue {
    settings: Record<string, unknown>;
}
