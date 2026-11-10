export const EMAIL_ADDRESS_INPUT_UI_ALIAS = "UmbracoAutomate.PropertyEditorUi.EmailAddressInput";

const emailAddressInput: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: EMAIL_ADDRESS_INPUT_UI_ALIAS,
    name: "Automate Email Address Input",
    element: () => import("./email-address-input.element.js"),
    meta: {
        label: "Email Address Input",
        icon: "icon-message",
        group: "Automate",
    },
};

export const emailAddressInputManifests: UmbExtensionManifest[] = [emailAddressInput];
