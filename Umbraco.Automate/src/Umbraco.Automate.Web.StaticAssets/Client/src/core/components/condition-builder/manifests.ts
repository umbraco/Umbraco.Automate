export const CONDITION_BUILDER_UI_ALIAS = "UmbracoAutomate.PropertyEditorUi.ConditionBuilder";

const conditionBuilder: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: CONDITION_BUILDER_UI_ALIAS,
    name: "Automate Condition Builder",
    element: () => import("./condition-builder.element.js"),
    meta: {
        label: "Condition Builder",
        icon: "icon-split",
        group: "Automate",
    },
};

export const conditionBuilderManifests: UmbExtensionManifest[] = [conditionBuilder];
