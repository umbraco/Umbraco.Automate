export const MCP_INPUT_FIELDS_BUILDER_UI_ALIAS = "UmbracoAutomate.PropertyEditorUi.McpInputFieldsBuilder";

const mcpInputFieldsBuilder: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: MCP_INPUT_FIELDS_BUILDER_UI_ALIAS,
    name: "Automate MCP Input Fields Builder",
    element: () => import("./mcp-input-fields-builder.element.js"),
    meta: {
        label: "MCP Input Fields Builder",
        icon: "icon-plug",
        group: "Automate",
    },
};

export const mcpInputFieldsBuilderManifests: UmbExtensionManifest[] = [mcpInputFieldsBuilder];
