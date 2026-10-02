/**
 * The shape a binding is stored in. A field whose editor stores an array (a multi-value
 * picker, or a dropdown, which always stores `["x"]`) keeps the binding as a one-item array
 * so the stored JSON still deserializes into the settings property: the server resolves each
 * string in a `List<string>` on its own, and flattens a one-item array into a `string`.
 */
export type UaBindingValueShape = "string" | "array";

/**
 * Whether a string holds a binding expression rather than a literal value.
 *
 * Deliberately loose: any `${ ... }` anywhere in the string counts. A field holding a partial
 * or half-typed expression still belongs in binding mode, otherwise switching away from the
 * picker would look like the value was discarded.
 */
export function isBindingExpression(value: unknown): value is string {
    return typeof value === "string" && /\$\{[^}]*\}/.test(value);
}

/**
 * Whether a stored settings value is a binding, in either shape: a `${ }` string, or a
 * one-item array holding one.
 */
export function isBindingValue(value: unknown): boolean {
    return getBindingExpression(value) !== undefined;
}

/** The expression text of a binding value, or undefined when the value is not a binding. */
export function getBindingExpression(value: unknown): string | undefined {
    if (isBindingExpression(value)) return value;
    if (Array.isArray(value) && value.length === 1 && isBindingExpression(value[0])) return value[0];
    return undefined;
}

/** Wraps an expression in the shape the field stores its value in. */
export function toBindingValue(expression: string, shape: UaBindingValueShape): string | string[] {
    return shape === "array" ? [expression] : expression;
}

/** Whether a value counts as "nothing entered", for mandatory validation across both shapes. */
export function isEmptySettingsValue(value: unknown): boolean {
    if (value === undefined || value === null) return true;
    if (typeof value === "string") return value.trim() === "";
    if (Array.isArray(value)) return value.length === 0;
    return false;
}
