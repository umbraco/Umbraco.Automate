import type { Condition } from "../../../core/components/condition-builder/types.js";
import type { BindingSource } from "../../../core/utils/binding-context.utils.js";

export interface UaConditionModalData {
    condition: Condition;
    bindingSources: BindingSource[];
}

export interface UaConditionModalValue {
    condition: Condition;
}
