import type { ManifestPropertyEditorUi } from "@umbraco-cms/backoffice/property-editor";

const propertyEditorUi: ManifestPropertyEditorUi = {
    type: "propertyEditorUi",
    alias: "Umb.Automate.OAuth",
    name: "OAuth Connection Property Editor UI",
    element: () => import("./property-editor-ui-oauth.element.js"),
    meta: {
        label: "OAuth Connection",
        icon: "icon-lock",
        group: "Umbraco Automate",
    },
};

export const oauthPropertyEditorManifests = [propertyEditorUi];
