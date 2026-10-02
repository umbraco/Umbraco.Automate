export const SWITCH_CASE_BUILDER_UI_ALIAS = "UmbracoAutomate.PropertyEditorUi.SwitchCaseBuilder";

const switchCaseBuilder: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: SWITCH_CASE_BUILDER_UI_ALIAS,
    name: "Automate Switch Case Builder",
    element: () => import("./switch-case-builder.element.js"),
    meta: {
        label: "Switch Case Builder",
        icon: "icon-merge",
        group: "Automate",
    },
};

export const switchCaseBuilderManifests: UmbExtensionManifest[] = [switchCaseBuilder];
