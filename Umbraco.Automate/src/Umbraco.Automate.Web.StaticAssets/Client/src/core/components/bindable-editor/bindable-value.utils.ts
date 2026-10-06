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

const GUID = "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";
const GUID_TEXT = new RegExp(`^(?:\\{${GUID}\\}|${GUID})$`, "i");

/**
 * Whether a value is a GUID written out as text: the whole trimmed string, any case, with braces
 * either both present or both absent. Use `normalizeGuid` to turn a match into canonical form.
 */
export function isGuidText(value: unknown): value is string {
    return typeof value === "string" && GUID_TEXT.test(value.trim());
}

/** Canonical form of a GUID text accepted by `isGuidText`: trimmed, no braces, lower case. */
export function normalizeGuid(value: string): string {
    return value.trim().replace(/^\{|\}$/g, "").toLowerCase();
}

/** Whether a value counts as "nothing entered", for mandatory validation. */
export function isEmptySettingsValue(value: unknown): boolean {
    if (value === undefined || value === null) return true;
    if (typeof value === "string") return value.trim() === "";
    if (Array.isArray(value)) return value.length === 0;
    return false;
}
