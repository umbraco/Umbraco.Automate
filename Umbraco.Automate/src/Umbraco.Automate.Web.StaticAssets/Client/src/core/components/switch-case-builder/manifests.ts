const switchCaseBuilder: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: "UmbracoAutomate.PropertyEditorUi.SwitchCaseBuilder",
    name: "Automate Switch Case Builder",
    element: () => import("./switch-case-builder.element.js"),
    meta: {
        label: "Switch Case Builder",
        icon: "icon-merge",
        group: "Automate",
    },
};

export const switchCaseBuilderManifests: UmbExtensionManifest[] = [switchCaseBuilder];
