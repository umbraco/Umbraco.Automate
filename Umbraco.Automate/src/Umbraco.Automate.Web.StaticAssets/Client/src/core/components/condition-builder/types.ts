export type ConditionOperator =
    | "Equals"
    | "NotEquals"
    | "Contains"
    | "NotContains"
    | "StartsWith"
    | "EndsWith"
    | "GreaterThan"
    | "LessThan"
    | "GreaterThanOrEquals"
    | "LessThanOrEquals"
    | "IsEmpty"
    | "IsNotEmpty";

export interface Condition {
    LeftOperand: string;
    Operator: ConditionOperator;
    RightOperand: string;
}

export interface ConditionGroup {
    Conditions: Condition[];
}

export interface ConditionSet {
    Groups: ConditionGroup[];
}

export const UNARY_OPERATORS: ConditionOperator[] = ["IsEmpty", "IsNotEmpty"];
