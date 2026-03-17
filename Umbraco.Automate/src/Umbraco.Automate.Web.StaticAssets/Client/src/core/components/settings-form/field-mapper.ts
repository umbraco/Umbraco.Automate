/**
 * Maps EditorUiAlias or propertyType to a simple editor type for rendering.
 *
 * When an EditorUiAlias is specified, it takes precedence.
 * Otherwise, the CLR type name is used to infer the editor.
 */

export type FieldEditorType = "text" | "textarea" | "number" | "toggle" | "password";

const EDITOR_UI_ALIAS_MAP: Record<string, FieldEditorType> = {
    "Umb.PropertyEditorUi.TextBox": "text",
    "Umb.PropertyEditorUi.TextArea": "textarea",
    "Umb.PropertyEditorUi.Integer": "number",
    "Umb.PropertyEditorUi.Decimal": "number",
    "Umb.PropertyEditorUi.Toggle": "toggle",
};

const PROPERTY_TYPE_MAP: Record<string, FieldEditorType> = {
    String: "text",
    Boolean: "toggle",
    Int32: "number",
    Int64: "number",
    Double: "number",
    Decimal: "number",
    Single: "number",
};

export function resolveEditorType(
    editorUiAlias: string | null | undefined,
    propertyType: string,
    isSensitive: boolean,
): FieldEditorType {
    if (isSensitive) return "password";

    if (editorUiAlias) {
        const mapped = EDITOR_UI_ALIAS_MAP[editorUiAlias];
        if (mapped) return mapped;
    }

    return PROPERTY_TYPE_MAP[propertyType] ?? "text";
}
