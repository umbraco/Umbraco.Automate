const conditionBuilder: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: "UmbracoAutomate.PropertyEditorUi.ConditionBuilder",
    name: "Automate Condition Builder",
    element: () => import("./condition-builder.element.js"),
    meta: {
        label: "Condition Builder",
        icon: "icon-split",
        group: "Automate",
    },
};

export const conditionBuilderManifests: UmbExtensionManifest[] = [conditionBuilder];
