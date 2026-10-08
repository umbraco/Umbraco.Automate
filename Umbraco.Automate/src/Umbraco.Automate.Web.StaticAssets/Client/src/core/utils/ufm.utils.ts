// Fenced code blocks and inline code spans. UFM never evaluates `${ … }` inside these, and marked
// renders their contents literally, so escaping there would show the entity as text.
const MARKDOWN_CODE = /(```[\s\S]*?```|`[^`\n]*`)/;

/**
 * Stops the backoffice's UFM renderer from evaluating `${ … }` in text it renders as markdown, such as
 * an `umb-property` description, so binding syntax shows as written.
 *
 * UFM treats `${ … }` as a JavaScript expression and evaluates it against the property, so an example
 * like `${ trigger.items }` renders as nothing. A backslash does not escape it (marked consumes the
 * backslash before the UFM tokenizer runs), but an HTML entity does: the tokenizer no longer sees `${`,
 * and the browser still shows `$`. Apply it after localizing, so `#key` terms still resolve and a
 * localized string gets the same treatment.
 *
 * Inline code spans and fenced code blocks are left alone: UFM does not evaluate inside them, and
 * marked would render the entity there as literal text.
 */
export function escapeUfmExpressions(text: string): string {
    return text
        .split(MARKDOWN_CODE)
        .map((part, index) => (index % 2 === 1 ? part : part.replace(/\$\{/g, "&#36;{")))
        .join("");
}
