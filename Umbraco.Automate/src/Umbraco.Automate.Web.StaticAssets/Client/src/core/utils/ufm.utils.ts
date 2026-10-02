/**
 * Stops the backoffice's UFM renderer from evaluating `${ … }` in text it renders as markdown, such as
 * an `umb-property` description, so binding syntax shows as written.
 *
 * UFM treats `${ … }` as a JavaScript expression and evaluates it against the property, so an example
 * like `${ trigger.items }` renders as nothing. A backslash does not escape it (marked consumes the
 * backslash before the UFM tokenizer runs), but an HTML entity does: the tokenizer no longer sees `${`,
 * and the browser still shows `$`. Apply it after localizing, so `#key` terms still resolve and a
 * localized string gets the same treatment.
 */
export function escapeUfmExpressions(text: string): string {
    return text.replace(/\$\{/g, "&#36;{");
}
