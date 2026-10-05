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

/** The expression text of a binding value, or undefined when the value is not a binding. */
export function getBindingExpression(value: unknown): string | undefined {
    return isBindingExpression(value) ? value : undefined;
}

/** Whether a value counts as "nothing entered", for mandatory validation. */
export function isEmptySettingsValue(value: unknown): boolean {
    if (value === undefined || value === null) return true;
    if (typeof value === "string") return value.trim() === "";
    if (Array.isArray(value)) return value.length === 0;
    return false;
}
