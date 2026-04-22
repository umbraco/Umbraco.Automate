// Registers Deploy entity actions (Queue for transfer / Tree restore / Partial restore)
// against Automate's tree entity types. Mirrors the pattern in Umbraco.Forms.Deploy.

const UA_WORKSPACE_ENTITY_TYPE = "ua:workspace";
const UA_WORKSPACE_ROOT_ENTITY_TYPE = "ua:workspace-root";
const UA_AUTOMATION_ENTITY_TYPE = "ua:automation";
const UA_AUTOMATION_ROOT_ENTITY_TYPE = "ua:automation-root";
const UA_AUTOMATION_GROUP_ENTITY_TYPE = "ua:automation-group";

// Automate's tree uses client entity types prefixed with "ua:", but Deploy registers
// transfer options server-side against UDI entity types. This mapping lets Deploy's
// lookup (`DeployContext.getRegisteredEntity`) resolve a tree entity type to the
// matching server registration.
const UA_SERVER_WORKSPACE_ENTITY_TYPE = "umbraco-automate-workspace";
const UA_SERVER_WORKSPACE_GROUP_ENTITY_TYPE = "umbraco-automate-workspace-group";
const UA_SERVER_AUTOMATION_ENTITY_TYPE = "umbraco-automate-automation";

export const onInit = async (host, extensionRegistry) => {
  extensionRegistry.registerMany([
    {
      type: "localization",
      alias: "Automate.Deploy.Localization.En",
      weight: -100,
      name: "English",
      meta: {
        culture: "en",
        localizations: {
          deploy_entityTypes: {
            [UA_WORKSPACE_ENTITY_TYPE]: "Automate workspace",
            [UA_AUTOMATION_ENTITY_TYPE]: "Automation",
            [UA_AUTOMATION_GROUP_ENTITY_TYPE]: "Automation folder",
          },
        },
      },
    },
    {
      type: "deployEntityTypeMapping",
      alias: "Automate.Deploy.EntityTypeMapping",
      name: "Automate Deploy Entity Type Mapping",
      entityTypes: {
        [UA_WORKSPACE_ENTITY_TYPE]: UA_SERVER_WORKSPACE_ENTITY_TYPE,
        [UA_AUTOMATION_ENTITY_TYPE]: UA_SERVER_AUTOMATION_ENTITY_TYPE,
        [UA_AUTOMATION_GROUP_ENTITY_TYPE]: UA_SERVER_WORKSPACE_GROUP_ENTITY_TYPE,
      },
    },
    {
      type: "deployEntityActionRegistrar",
      actionAlias: "Deploy.EntityAction.EnvironmentRestore",
      alias: "Automate.Deploy.EnvironmentRestore.Registrar",
      name: "Automate Deploy Environment Restore Entity Action Registrar",
      forEntityTypes: [
        {
          entityTypes: [UA_WORKSPACE_ROOT_ENTITY_TYPE],
          conditions: ["default"],
        },
      ],
    },
    {
      type: "deployEntityActionRegistrar",
      actionAlias: "Deploy.EntityAction.EnvironmentExport",
      alias: "Automate.Deploy.EnvironmentExport.Registrar",
      name: "Automate Deploy Environment Export Entity Action Registrar",
      forEntityTypes: [
        {
          entityTypes: [UA_WORKSPACE_ROOT_ENTITY_TYPE],
          conditions: ["default"],
        },
      ],
    },
    {
      type: "deployEntityActionRegistrar",
      actionAlias: "Deploy.EntityAction.Queue",
      alias: "Automate.Deploy.Queue.Registrar",
      name: "Automate Deploy Queue Entity Action Registrar",
      forEntityTypes: [
        {
          entityTypes: [
            UA_WORKSPACE_ROOT_ENTITY_TYPE,
            UA_WORKSPACE_ENTITY_TYPE,
            UA_AUTOMATION_ROOT_ENTITY_TYPE,
            UA_AUTOMATION_ENTITY_TYPE,
            UA_AUTOMATION_GROUP_ENTITY_TYPE,
          ],
          conditions: ["default"],
        },
      ],
    },
    {
      type: "deployEntityActionRegistrar",
      actionAlias: "Deploy.EntityAction.TreeRestore",
      alias: "Automate.Deploy.TreeRestore.Registrar",
      name: "Automate Deploy Tree Restore Entity Action Registrar",
      forEntityTypes: [
        {
          entityTypes: [
            UA_WORKSPACE_ROOT_ENTITY_TYPE,
            UA_AUTOMATION_ROOT_ENTITY_TYPE,
          ],
          conditions: ["default"],
        },
      ],
    },
    {
      type: "deployEntityActionRegistrar",
      actionAlias: "Deploy.EntityAction.PartialRestore",
      alias: "Automate.Deploy.PartialRestore.Registrar",
      name: "Automate Deploy Partial Restore Entity Action Registrar",
      forEntityTypes: [
        {
          entityTypes: [
            UA_WORKSPACE_ROOT_ENTITY_TYPE,
            UA_WORKSPACE_ENTITY_TYPE,
            UA_AUTOMATION_ROOT_ENTITY_TYPE,
            UA_AUTOMATION_ENTITY_TYPE,
            UA_AUTOMATION_GROUP_ENTITY_TYPE,
          ],
          conditions: ["default"],
        },
      ],
    },
  ]);
};
