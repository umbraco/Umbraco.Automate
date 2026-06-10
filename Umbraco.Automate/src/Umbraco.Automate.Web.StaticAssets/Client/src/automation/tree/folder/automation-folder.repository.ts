import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbDetailRepositoryBase } from "@umbraco-cms/backoffice/repository";
import type { UmbFolderModel } from "@umbraco-cms/backoffice/tree";

import { UaAutomationFolderServerDataSource } from "./automation-folder.server.data-source.js";
import { UA_AUTOMATION_FOLDER_STORE_CONTEXT } from "./automation-folder.store.js";

export class UaAutomationFolderRepository extends UmbDetailRepositoryBase<UmbFolderModel> {
    constructor(host: UmbControllerHost) {
        super(host, UaAutomationFolderServerDataSource, UA_AUTOMATION_FOLDER_STORE_CONTEXT);
    }
}

export { UaAutomationFolderRepository as api };
