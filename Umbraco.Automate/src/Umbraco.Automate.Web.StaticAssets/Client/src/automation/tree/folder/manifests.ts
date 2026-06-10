import { UA_AUTOMATION_GROUP_ENTITY_TYPE } from "../../entity.js";
import { UA_AUTOMATION_FOLDER_REPOSITORY_ALIAS, UA_AUTOMATION_FOLDER_STORE_ALIAS } from "./constants.js";

export const automationFolderManifests: Array<UmbExtensionManifest> = [
    {
        type: "repository",
        alias: UA_AUTOMATION_FOLDER_REPOSITORY_ALIAS,
        name: "Automation Folder Repository",
        api: () => import("./automation-folder.repository.js"),
    },
    {
        type: "store",
        alias: UA_AUTOMATION_FOLDER_STORE_ALIAS,
        name: "Automation Folder Store",
        api: () => import("./automation-folder.store.js"),
    },
    {
        type: "entityAction",
        kind: "folderUpdate",
        alias: "UmbracoAutomate.EntityAction.Automation.Folder.Rename",
        name: "Rename Automation Folder Entity Action",
        forEntityTypes: [UA_AUTOMATION_GROUP_ENTITY_TYPE],
        meta: {
            folderRepositoryAlias: UA_AUTOMATION_FOLDER_REPOSITORY_ALIAS,
        },
    },
    {
        type: "entityAction",
        kind: "folderDelete",
        alias: "UmbracoAutomate.EntityAction.Automation.Folder.Delete",
        name: "Delete Automation Folder Entity Action",
        forEntityTypes: [UA_AUTOMATION_GROUP_ENTITY_TYPE],
        meta: {
            folderRepositoryAlias: UA_AUTOMATION_FOLDER_REPOSITORY_ALIAS,
        },
    },
];
