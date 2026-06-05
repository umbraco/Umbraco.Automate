export const USER_GROUP_PICKER_UI_ALIAS = "Umb.Automate.UserGroupPicker";

const userGroupPicker: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: USER_GROUP_PICKER_UI_ALIAS,
    name: "Automate User Group Picker",
    element: () => import("./user-group-picker.element.js"),
    meta: {
        label: "User Group Picker",
        icon: "icon-users",
        group: "Automate",
    },
};

export const userGroupPickerManifests: UmbExtensionManifest[] = [
    userGroupPicker,
];
