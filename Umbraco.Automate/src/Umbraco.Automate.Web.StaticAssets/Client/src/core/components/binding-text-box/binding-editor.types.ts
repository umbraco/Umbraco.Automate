/**
 * Shape implemented by binding-aware property editor elements (binding-text-box,
 * binding-text-area) so the "Insert binding" property action can splice the
 * picked `${ }` expression in at the current caret position rather than
 * appending it.
 */
export interface UaBindingInsertable {
    insertAtCaret(expression: string): void;
}

export function isBindingInsertable(target: unknown): target is UaBindingInsertable {
    return (
        target !== null &&
        typeof target === "object" &&
        typeof (target as { insertAtCaret?: unknown }).insertAtCaret === "function"
    );
}
