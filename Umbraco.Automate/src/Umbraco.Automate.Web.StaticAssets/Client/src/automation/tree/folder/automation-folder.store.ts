import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbDetailStoreBase } from "@umbraco-cms/backoffice/store";
import { UmbContextToken } from "@umbraco-cms/backoffice/context-api";
import type { UmbFolderModel } from "@umbraco-cms/backoffice/tree";
import { UA_AUTOMATION_FOLDER_STORE_ALIAS } from "./constants.js";

export class UaAutomationFolderStore extends UmbDetailStoreBase<UmbFolderModel> {
    constructor(host: UmbControllerHost) {
        super(host, UA_AUTOMATION_FOLDER_STORE_ALIAS);
    }
}

export default UaAutomationFolderStore;

export const UA_AUTOMATION_FOLDER_STORE_CONTEXT = new UmbContextToken<UaAutomationFolderStore>(
    UA_AUTOMATION_FOLDER_STORE_ALIAS,
);
