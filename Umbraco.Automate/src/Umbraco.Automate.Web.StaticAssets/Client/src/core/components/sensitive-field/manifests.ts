export const SENSITIVE_FIELD_UI_ALIAS = "Umb.Automate.SensitiveField";

const sensitiveField: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: SENSITIVE_FIELD_UI_ALIAS,
    name: "Automate Sensitive Field",
    element: () => import("./sensitive-field.element.js"),
    meta: {
        label: "Sensitive Field",
        icon: "icon-lock",
        group: "Automate",
    },
};

export const sensitiveFieldManifests: UmbExtensionManifest[] = [
    sensitiveField,
];
