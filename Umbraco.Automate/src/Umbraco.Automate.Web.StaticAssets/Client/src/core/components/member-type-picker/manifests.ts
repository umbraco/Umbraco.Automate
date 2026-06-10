export const MEMBER_TYPE_PICKER_UI_ALIAS = "Umb.Automate.MemberTypePicker";

const memberTypePicker: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: MEMBER_TYPE_PICKER_UI_ALIAS,
    name: "Automate Member Type Picker",
    element: () => import("./member-type-picker.element.js"),
    meta: {
        label: "Member Type Picker",
        icon: "icon-user",
        group: "Automate",
    },
};

export const memberTypePickerManifests: UmbExtensionManifest[] = [
    memberTypePicker,
];
